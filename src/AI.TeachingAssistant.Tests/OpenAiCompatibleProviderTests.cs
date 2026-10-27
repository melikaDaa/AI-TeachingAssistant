using System.Net;
using System.Text.Json;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AI.TeachingAssistant.Tests;

/// <summary>
/// Phase 2 token accounting + Phase 3 controlled failover: the OpenAI-compatible provider
/// must surface the gateway's usage object (never invent one when absent) and — only for
/// genuine transient availability failures (timeout / network / 429 / 5xx) — retry once via
/// the configured fallback model with the SAME request contract. On the normal path the
/// primary succeeds and the fallback is never called; non-transient failures never fail over.
/// </summary>
public class OpenAiCompatibleProviderTests
{
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public string Messages { get; private set; } = string.Empty;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages += formatter(state, exception) + "\n";
    }

    /// <summary>Serves queued responses in order and records every request body it saw.</summary>
    private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private int _next;
        public List<string> RequestBodies { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBodies.Add(request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult());
            var response = responses[Math.Min(_next, responses.Length - 1)];
            _next++;
            return Task.FromResult(response);
        }
    }

    /// <summary>Simulates the timeout category: the first call dies like an aborted request.</summary>
    private sealed class TimeoutThenOkHandler : HttpMessageHandler
    {
        private int _calls;
        public List<string> RequestBodies { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBodies.Add(request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult());
            if (_calls++ == 0)
                throw new OperationCanceledException();
            return Task.FromResult(Ok());
        }
    }

    private const string PayloadWithUsage = """
        {
          "choices": [ { "message": { "role": "assistant", "content": "{\"learningObjectives\":[\"x\"]}" } } ],
          "usage": { "prompt_tokens": 812, "completion_tokens": 356, "total_tokens": 1168 }
        }
        """;

    private const string PayloadWithoutUsage = """
        {
          "choices": [ { "message": { "role": "assistant", "content": "{\"learningObjectives\":[\"x\"]}" } } ]
        }
        """;

    private const string PayloadEmptyContent = """
        {
          "choices": [ { "message": { "role": "assistant", "content": "" } } ]
        }
        """;

    private static HttpResponseMessage Ok(string body = PayloadWithUsage) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static HttpResponseMessage Status(HttpStatusCode code) =>
        new(code) { Content = new StringContent("{}") };

    private static OpenAiCompatibleProvider Provider(HttpMessageHandler handler, string? fallback = null) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://gateway.test/v1/") },
        new OpenAiCompatibleOptions
        {
            ApiKey = "test-key",
            Model = "primary-model",
            FallbackModel = fallback ?? string.Empty,
            TimeoutSeconds = 10,
        },
        NullLogger<OpenAiCompatibleProvider>.Instance);

    private sealed record SeenRequest(string Model, double Temperature, int MaxTokens, int MessageCount);

    private static SeenRequest ParseRequest(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        return new SeenRequest(
            root.GetProperty("model").GetString()!,
            root.GetProperty("temperature").GetDouble(),
            root.GetProperty("maxTokens").GetInt32(), // JsonContent.Create uses web (camelCase) defaults
            root.GetProperty("messages").GetArrayLength());
    }

    // ---------- Phase 2 (unchanged) ----------

    [Fact]
    public async Task Usage_WhenProviderReportsIt_IsParsedIntoResponse()
    {
        var handler = new QueueHandler(Ok(PayloadWithUsage));
        var result = await Provider(handler).CompleteAsync(new AiRequest("s", "u"));

        Assert.NotNull(result.Usage);
        Assert.Equal(812, result.Usage.PromptTokens);
        Assert.Equal(356, result.Usage.CompletionTokens);
        Assert.Equal(1168, result.Usage.TotalTokens);
    }

    [Fact]
    public async Task Usage_WhenProviderOmitsIt_StaysNull()
    {
        var handler = new QueueHandler(Ok(PayloadWithoutUsage));
        var result = await Provider(handler).CompleteAsync(new AiRequest("s", "u"));

        Assert.Null(result.Usage);
    }

    // ---------- Phase 3: normal path ----------

    [Fact]
    public async Task PrimarySucceeds_FallbackIsNeverCalled()
    {
        var handler = new QueueHandler(Ok());
        var result = await Provider(handler, fallback: "fallback-model").CompleteAsync(new AiRequest("s", "u"));

        Assert.Equal("{\"learningObjectives\":[\"x\"]}", result.Content);
        Assert.Single(handler.RequestBodies);
        Assert.Equal("primary-model", ParseRequest(handler.RequestBodies[0]).Model);
    }

    // ---------- Phase 3: fallback-triggering failure categories ----------

    [Fact]
    public async Task PrimaryTimeout_FallsOverOnceToFallback()
    {
        var handler = new TimeoutThenOkHandler();
        var result = await Provider(handler, fallback: "fallback-model").CompleteAsync(new AiRequest("s", "u"));

        Assert.Equal("{\"learningObjectives\":[\"x\"]}", result.Content);
        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.Equal("primary-model", ParseRequest(handler.RequestBodies[0]).Model);
        Assert.Equal("fallback-model", ParseRequest(handler.RequestBodies[1]).Model);
    }

    [Fact]
    public async Task Primary503_ServiceUnavailable_FallsOverOnceToFallback()
    {
        var handler = new QueueHandler(Status(HttpStatusCode.ServiceUnavailable), Ok());
        var result = await Provider(handler, fallback: "fallback-model").CompleteAsync(new AiRequest("s", "u"));

        Assert.Equal("{\"learningObjectives\":[\"x\"]}", result.Content);
        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.Equal("fallback-model", ParseRequest(handler.RequestBodies[1]).Model);
    }

    [Fact]
    public async Task Primary500_FallsOverWithSameRequestContract()
    {
        var handler = new QueueHandler(Status(HttpStatusCode.InternalServerError), Ok());
        var request = new AiRequest("system prompt", "user prompt", MaxTokens: 1500, Temperature: 0.35);
        var result = await Provider(handler, fallback: "fallback-model").CompleteAsync(request);

        Assert.Equal("{\"learningObjectives\":[\"x\"]}", result.Content);

        var primary = ParseRequest(handler.RequestBodies[0]);
        var fallback = ParseRequest(handler.RequestBodies[1]);
        Assert.Equal("primary-model", primary.Model);
        Assert.Equal("fallback-model", fallback.Model);
        // Identical generation contract: only the model differs between the two attempts.
        Assert.Equal(primary.Temperature, fallback.Temperature);
        Assert.Equal(primary.MaxTokens, fallback.MaxTokens);
        Assert.Equal(primary.MessageCount, fallback.MessageCount);
        Assert.Equal(1500, fallback.MaxTokens);
        Assert.Equal(0.35, fallback.Temperature);
    }

    [Fact]
    public async Task Primary429_RateLimit_FallsOverOnceBounded()
    {
        var handler = new QueueHandler(Status(HttpStatusCode.TooManyRequests), Ok());
        var result = await Provider(handler, fallback: "fallback-model").CompleteAsync(new AiRequest("s", "u"));

        Assert.Equal("{\"learningObjectives\":[\"x\"]}", result.Content);
        Assert.Equal(2, handler.RequestBodies.Count);
    }

    [Fact]
    public async Task FallbackResponse_UsageIsReported()
    {
        var handler = new QueueHandler(Status(HttpStatusCode.InternalServerError), Ok(PayloadWithUsage));
        var result = await Provider(handler, fallback: "fallback-model").CompleteAsync(new AiRequest("s", "u"));

        Assert.NotNull(result.Usage);
        Assert.Equal(812, result.Usage.PromptTokens);
        Assert.Equal(1168, result.Usage.TotalTokens);
    }

    // ---------- Phase 3: failures that must NEVER fall over ----------

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task AuthFailure_NeverFallsOver(HttpStatusCode status)
    {
        var handler = new QueueHandler(Status(status), Ok());
        var ex = await Assert.ThrowsAsync<AiProviderException>(
            () => Provider(handler, fallback: "fallback-model").CompleteAsync(new AiRequest("s", "u")));

        Assert.False(ex.IsTransient);
        Assert.Single(handler.RequestBodies);
    }

    [Fact]
    public async Task MalformedResponse_NeverFallsOver()
    {
        var handler = new QueueHandler(Ok("{not json"));
        var ex = await Assert.ThrowsAsync<AiProviderException>(
            () => Provider(handler, fallback: "fallback-model").CompleteAsync(new AiRequest("s", "u")));

        Assert.False(ex.IsTransient);
        Assert.Single(handler.RequestBodies);
    }

    [Fact]
    public async Task EmptyContentResponse_NeverFallsOver()
    {
        var handler = new QueueHandler(Ok(PayloadEmptyContent));
        var ex = await Assert.ThrowsAsync<AiProviderException>(
            () => Provider(handler, fallback: "fallback-model").CompleteAsync(new AiRequest("s", "u")));

        Assert.False(ex.IsTransient);
        Assert.Single(handler.RequestBodies);
    }

    [Fact]
    public async Task MissingApiKey_NeverFallsOverAndNeverCallsHttp()
    {
        var handler = new QueueHandler(Ok());
        var provider = new OpenAiCompatibleProvider(
            new HttpClient(handler) { BaseAddress = new Uri("https://gateway.test/v1/") },
            new OpenAiCompatibleOptions { ApiKey = "", Model = "primary-model", FallbackModel = "fallback-model", TimeoutSeconds = 10 },
            NullLogger<OpenAiCompatibleProvider>.Instance);

        var ex = await Assert.ThrowsAsync<AiProviderException>(() => provider.CompleteAsync(new AiRequest("s", "u")));

        Assert.False(ex.IsTransient);
        Assert.Empty(handler.RequestBodies);
    }

    // ---------- Phase 3: bounded behaviour ----------

    [Fact]
    public async Task BothAttemptsFail_ControlledFailureNoLoop()
    {
        var handler = new QueueHandler(Status(HttpStatusCode.InternalServerError), Status(HttpStatusCode.InternalServerError));
        var ex = await Assert.ThrowsAsync<AiProviderException>(
            () => Provider(handler, fallback: "fallback-model").CompleteAsync(new AiRequest("s", "u")));

        Assert.True(ex.IsTransient);
        Assert.Equal(2, handler.RequestBodies.Count); // one primary + one fallback, nothing more
    }

    [Fact]
    public async Task UpstreamErrorBody_IsNotWrittenToLogs()
    {
        const string sentinel = "provider-echoed-sensitive-value";
        var handler = new QueueHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(sentinel),
        });
        var logger = new CapturingLogger<OpenAiCompatibleProvider>();
        var provider = new OpenAiCompatibleProvider(
            new HttpClient(handler) { BaseAddress = new Uri("https://gateway.test/v1/") },
            new OpenAiCompatibleOptions { ApiKey = "test-key", Model = "primary-model", TimeoutSeconds = 10 },
            logger);

        await Assert.ThrowsAsync<AiProviderException>(() => provider.CompleteAsync(new AiRequest("s", "u")));

        Assert.DoesNotContain(sentinel, logger.Messages);
        Assert.DoesNotContain("test-key", logger.Messages);
    }

    [Fact]
    public async Task NoFallbackConfigured_FailureIsTransientWithoutFailover()
    {
        var handler = new QueueHandler(Status(HttpStatusCode.InternalServerError));
        var ex = await Assert.ThrowsAsync<AiProviderException>(
            () => Provider(handler, fallback: null).CompleteAsync(new AiRequest("s", "u")));

        Assert.True(ex.IsTransient);
        Assert.Single(handler.RequestBodies);
    }

    [Fact]
    public async Task FallbackEqualsPrimary_FailoverStaysDisabled()
    {
        var handler = new QueueHandler(Status(HttpStatusCode.InternalServerError));
        var ex = await Assert.ThrowsAsync<AiProviderException>(
            () => Provider(handler, fallback: "primary-model").CompleteAsync(new AiRequest("s", "u")));

        Assert.True(ex.IsTransient);
        Assert.Single(handler.RequestBodies); // falling back to the same model would be pointless
    }
}

using System.Net;
using System.Text;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Infrastructure.Ai;
using Xunit;

namespace AI.TeachingAssistant.Tests;

/// <summary>
/// Contract tests for the asynchronous xKiro image provider (submit → poll → download).
/// All HTTP is faked; no network and no cost.
/// </summary>
public sealed class XKiroImageProviderTests
{
    private static ImageGenerationOptions Options() => new()
    {
        Provider = "xkiro",
        ApiKey = "test-key",
        BaseUrl = "https://api.xkiro.com/v1",
        Model = "sensenova/sensenova-u1.5-lite",
        PollTimeoutSeconds = 60,
        MaxImagesPerSection = 2,
    };

    private static XKiroImageProvider Provider(HttpMessageHandler handler, ImageGenerationOptions options)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.xkiro.com/v1/") };
        return new XKiroImageProvider(http, options);
    }

    [Fact]
    public void Reports_enabled_name_and_limit()
    {
        var provider = Provider(new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)), Options());

        Assert.True(provider.IsEnabled);
        Assert.Equal(2, provider.MaxImagesPerSection);
        Assert.Contains("sensenova", provider.Name);
    }

    [Fact]
    public void Disabled_without_key()
    {
        var options = Options();
        options.ApiKey = string.Empty;
        var provider = Provider(new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)), options);

        Assert.False(provider.IsEnabled);
    }

    [Fact]
    public async Task Submits_polls_then_downloads_bytes()
    {
        var handler = new StubHandler((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/images/generations"))
                return Json(HttpStatusCode.Accepted, """{"id":"job-1","status":"processing"}""");
            if (request.Method == HttpMethod.Get && path.Contains("/images/generations/"))
                return Json(HttpStatusCode.OK, """{"id":"job-1","status":"succeeded","data":[{"url":"https://cdn.xkiro.com/a.png"}]}""");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 9, 8, 7 }) };
        });

        var image = await Provider(handler, Options()).GenerateAsync(new ImageGenerationRequest("a plant cell"));

        Assert.Equal(new byte[] { 9, 8, 7 }, image.Data);
        Assert.Equal("image/png", image.ContentType); // download returned no content type → defaults to png
        // The submit and poll calls carry the bearer key; the CDN download need not.
        Assert.Equal("Bearer", handler.AuthSchemes[0]);
        Assert.Equal("Bearer", handler.AuthSchemes[1]);
    }

    [Fact]
    public async Task Blocked_prompt_throws_non_transient_persian_error()
    {
        var handler = new StubHandler((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/images/generations"))
                return Json(HttpStatusCode.Accepted, """{"id":"job-2","status":"processing"}""");
            return Json(HttpStatusCode.OK, """{"id":"job-2","status":"blocked","error":{"message":"refused"}}""");
        });

        var ex = await Assert.ThrowsAsync<AiProviderException>(
            () => Provider(handler, Options()).GenerateAsync(new ImageGenerationRequest("blocked")));

        Assert.False(ex.IsTransient);
        Assert.Contains("پرامپت", ex.Message);
    }

    [Fact]
    public async Task Unauthorized_throws_auth_error()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var ex = await Assert.ThrowsAsync<AiProviderException>(
            () => Provider(handler, Options()).GenerateAsync(new ImageGenerationRequest("x")));

        Assert.False(ex.IsTransient);
    }

    [Fact]
    public async Task Temporary_poll_and_download_errors_retry_same_job_without_resubmitting()
    {
        var submissions = 0;
        var polls = 0;
        var downloads = 0;
        var handler = new StubHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                submissions++;
                return Json(HttpStatusCode.Accepted, """{"id":"existing-job"}""");
            }
            if (request.RequestUri!.AbsolutePath.Contains("/images/generations/"))
            {
                if (++polls == 1)
                {
                    var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                    return limited;
                }
                return Json(HttpStatusCode.OK, """{"status":"succeeded","data":[{"url":"https://cdn.xkiro.com/a.png"}]}""");
            }
            if (++downloads == 1) throw new HttpRequestException("temporary network failure");
            Assert.Null(request.Headers.Authorization);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1 }) };
        });

        var image = await Provider(handler, Options()).GenerateAsync(new ImageGenerationRequest("x"));

        Assert.Single(image.Data);
        Assert.Equal(1, submissions);
        Assert.Equal(2, polls);
        Assert.Equal(2, downloads);
    }

    [Fact]
    public async Task Image_deadline_is_provider_failure_instead_of_worker_cancellation()
    {
        var options = Options();
        options.PollTimeoutSeconds = 1;
        var handler = new StubHandler((_, _) => Json(HttpStatusCode.Accepted, """{"id":"slow-job"}"""));

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            Provider(handler, options).GenerateAsync(new ImageGenerationRequest("x")));

        Assert.True(ex.IsTransient);
    }

    [Fact]
    public async Task Caller_cancellation_is_preserved()
    {
        using var cts = new CancellationTokenSource();
        var handler = new StubHandler((_, _) =>
        {
            cts.Cancel();
            return Json(HttpStatusCode.Accepted, """{"id":"cancelled-job"}""");
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Provider(handler, Options()).GenerateAsync(new ImageGenerationRequest("x"), cts.Token));
    }

    [Fact]
    public async Task Poll_auth_error_is_not_retried()
    {
        var calls = 0;
        var handler = new StubHandler((_, _) => ++calls == 1
            ? Json(HttpStatusCode.Accepted, """{"id":"job"}""")
            : new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            Provider(handler, Options()).GenerateAsync(new ImageGenerationRequest("x")));

        Assert.False(ex.IsTransient);
        Assert.Equal(2, calls);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    internal sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _responder;
        private int _count;

        public StubHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responder) => _responder = responder;

        public List<string?> AuthSchemes { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AuthSchemes.Add(request.Headers.Authorization?.Scheme);
            var n = Interlocked.Increment(ref _count);
            return Task.FromResult(_responder(request, n));
        }
    }
}

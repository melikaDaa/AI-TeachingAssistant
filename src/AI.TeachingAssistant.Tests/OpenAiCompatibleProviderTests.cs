using System.Net;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AI.TeachingAssistant.Tests;

/// <summary>
/// Phase 2 token accounting: the OpenAI-compatible provider must surface the
/// usage object the gateway reports — and must never invent one when absent.
/// </summary>
public class OpenAiCompatibleProviderTests
{
    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    private static OpenAiCompatibleProvider Provider(HttpResponseMessage response) => new(
        new HttpClient(new StubHandler(response)) { BaseAddress = new Uri("https://gateway.test/v1/") },
        new OpenAiCompatibleOptions { ApiKey = "test-key", Model = "test-model", TimeoutSeconds = 10 },
        NullLogger<OpenAiCompatibleProvider>.Instance);

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

    [Fact]
    public async Task Usage_WhenProviderReportsIt_IsParsedIntoResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PayloadWithUsage) };
        var result = await Provider(response).CompleteAsync(new AiRequest("s", "u"));

        Assert.NotNull(result.Usage);
        Assert.Equal(812, result.Usage.PromptTokens);
        Assert.Equal(356, result.Usage.CompletionTokens);
        Assert.Equal(1168, result.Usage.TotalTokens);
    }

    [Fact]
    public async Task Usage_WhenProviderOmitsIt_StaysNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PayloadWithoutUsage) };
        var result = await Provider(response).CompleteAsync(new AiRequest("s", "u"));

        Assert.Null(result.Usage);
    }
}

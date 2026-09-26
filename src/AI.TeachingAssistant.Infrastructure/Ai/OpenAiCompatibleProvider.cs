using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AI.TeachingAssistant.Application.Ai;
using AI.TeachingAssistant.Application.Common;
using Microsoft.Extensions.Configuration;

namespace AI.TeachingAssistant.Infrastructure.Ai;

/// <summary>Configuration for the OpenAI-compatible provider. Comes only from configuration/env vars.</summary>
public sealed class OpenAiCompatibleOptions
{
    public const string SectionName = "AI";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "gpt-4o-mini";
    public int TimeoutSeconds { get; set; } = 120;
    public int MaxTokens { get; set; } = 2000;
}

/// <summary>
/// Single concrete provider for the MVP: any OpenAI-compatible chat-completions endpoint
/// (OpenAI, Azure-style gateways, Iranian OpenAI-compatible services, ...).
/// Base URL and model are configurable, so swapping providers needs no code change.
/// </summary>
public class OpenAiCompatibleProvider : IAIProvider
{
    private readonly HttpClient _http;
    private readonly OpenAiCompatibleOptions _options;

    public OpenAiCompatibleProvider(HttpClient http, OpenAiCompatibleOptions options)
    {
        _http = http;
        _options = options;
    }

    public string Name => $"OpenAI-compatible ({_options.Model})";

    public async Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new AiProviderException("AI provider is not configured (missing API key).");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        var body = new ChatRequest(
            Model: _options.Model,
            Messages: new[]
            {
                new ChatMessage("system", request.SystemPrompt),
                new ChatMessage("user", request.UserPrompt),
            },
            Temperature: request.Temperature,
            MaxTokens: Math.Min(request.MaxTokens, _options.MaxTokens));

        HttpResponseMessage response;
        try
        {
            using var http = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            http.Content = JsonContent.Create(body);
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            response = await _http.SendAsync(http, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiProviderException("AI request timed out.");
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderException("AI network error.", ex);
        }

        if ((int)response.StatusCode == 429)
            throw new AiProviderException("AI rate limit reached.");
        if (!response.IsSuccessStatusCode)
            throw new AiProviderException($"AI provider returned status {(int)response.StatusCode}.");

        var payload = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: timeoutCts.Token);
        var content = payload?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
            throw new AiProviderException("AI returned an empty response.");

        return new AiResponse(content);
    }

    private sealed record ChatRequest(string Model, ChatMessage[] Messages, double Temperature, int MaxTokens);
    private sealed record ChatMessage(string Role, string Content);

    private sealed class ChatResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatChoice>? Choices { get; set; }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; set; }
    }
}

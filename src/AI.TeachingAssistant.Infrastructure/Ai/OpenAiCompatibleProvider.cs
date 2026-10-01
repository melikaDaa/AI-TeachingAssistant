using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AI.TeachingAssistant.Application.Ai;
using AI.TeachingAssistant.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AI.TeachingAssistant.Infrastructure.Ai;

/// <summary>Configuration for the OpenAI-compatible provider. Comes only from configuration/env vars.</summary>
public sealed class OpenAiCompatibleOptions
{
    public const string SectionName = "AI";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.xkiro.com/v1";
    public string Model { get; set; } = "qwen/qwen3.5-flash:free";  // vendor/model — must exist in the gateway catalog
    public int TimeoutSeconds { get; set; } = 110;               // xKiro blocks requests at ~95s; keep headroom above it
    public int MaxTokens { get; set; } = 3000;
}

/// <summary>
/// OpenAI-compatible chat-completions provider. Works with xKiro (https://api.xkiro.com/v1),
/// OpenAI, or any compatible gateway — Base URL and model come from configuration only.
/// The API key never leaves the backend and is never logged.
/// </summary>
public class OpenAiCompatibleProvider : IAIProvider
{
    private readonly HttpClient _http;
    private readonly OpenAiCompatibleOptions _options;
    private readonly ILogger<OpenAiCompatibleProvider>? _logger;

    public OpenAiCompatibleProvider(HttpClient http, OpenAiCompatibleOptions options, ILogger<OpenAiCompatibleProvider>? logger = null)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public string Name => $"OpenAI-compatible ({_options.Model})";

    public async Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new AiProviderException("کلید دسترسی سرویس هوش مصنوعی تنظیم نشده است.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        // Diagnostics only: sizes/lengths are logged, never prompt content.
        _logger?.LogInformation(
            "AI request via {Model}: systemChars={System} userChars={User} maxTokens={Max}",
            _options.Model, request.SystemPrompt.Length, request.UserPrompt.Length, request.MaxTokens);

        var body = new ChatRequest(
            Model: _options.Model,
            Messages: new[]
            {
                new ChatMessage("system", request.SystemPrompt),
                new ChatMessage("user", request.UserPrompt),
            },
            Temperature: request.Temperature,
            MaxTokens: Math.Min(request.MaxTokens, _options.MaxTokens));

        var startedAt = DateTime.UtcNow;
        HttpResponseMessage response;
        try
        {
            using var http = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            http.Content = JsonContent.Create(body);
            // Per-request auth header: DefaultRequestHeaders on a pooled typed client is shared
            // across concurrent calls and must never be mutated.
            http.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            response = await _http.SendAsync(http, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiProviderException("پاسخ سرویس هوش مصنوعی بیش از حد طول کشید.", isTransient: true);
        }
        catch (HttpRequestException)
        {
            throw new AiProviderException("ارتباط با سرویس هوش مصنوعی برقرار نشد. لطفاً دوباره تلاش کنید.", isTransient: true);
        }

        if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403)
            throw new AiProviderException("احراز هویت سرویس هوش مصنوعی ناموفق بود.");
        if ((int)response.StatusCode == 429)
            throw new AiProviderException("تعداد درخواست‌ها بیش از حد مجاز است. لطفاً کمی بعد دوباره تلاش کنید.", isTransient: true);
        if (!response.IsSuccessStatusCode)
        {
            // Server-side diagnostics: upstream error body (truncated) is logged for debugging;
            // the teacher only ever sees the generic Persian message.
            string? errBody = null;
            try { errBody = await response.Content.ReadAsStringAsync(timeoutCts.Token); }
            catch { /* body unreadable — status code is enough */ }
            _logger?.LogError(
                "AI provider returned {Status} after {Ms} ms: {Body}",
                (int)response.StatusCode,
                (long)(DateTime.UtcNow - startedAt).TotalMilliseconds,
                errBody is null ? "<unreadable>" : errBody.Length > 500 ? errBody[..500] : errBody);
            throw new AiProviderException("سرویس هوش مصنوعی در پاسخ‌دهی به مشکل خورد. لطفاً دوباره تلاش کنید.", isTransient: true);
        }

        ChatResponse? payload;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: timeoutCts.Token);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new AiProviderException("پاسخ سرویس هوش مصنوعی با ساختار مورد انتظار سازگار نبود.");
        }

        var content = payload?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
            throw new AiProviderException("پاسخ سرویس هوش مصنوعی با ساختار مورد انتظار سازگار نبود.");

        // Token accounting (Phase 2): OpenAI-compatible gateways report per-call usage.
        // Logged only — never fabricated when the gateway omits the field.
        var usage = payload?.Usage;
        if (usage is not null)
            _logger?.LogInformation(
                "AI response in {Ms} ms: promptTokens={Prompt} completionTokens={Completion} totalTokens={Total}",
                (long)(DateTime.UtcNow - startedAt).TotalMilliseconds,
                usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens);
        else
            _logger?.LogInformation(
                "AI response in {Ms} ms: token usage not reported by provider",
                (long)(DateTime.UtcNow - startedAt).TotalMilliseconds);

        return new AiResponse(content, usage is null
            ? null
            : new AiUsage(usage.PromptTokens ?? 0, usage.CompletionTokens ?? 0, usage.TotalTokens ?? 0));
    }

    private sealed record ChatRequest(string Model, ChatMessage[] Messages, double Temperature, int MaxTokens);
    private sealed record ChatMessage(string Role, string Content);

    private sealed class ChatResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatChoice>? Choices { get; set; }

        [JsonPropertyName("usage")]
        public ChatUsage? Usage { get; set; }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; set; }
    }

    private sealed class ChatUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int? PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int? CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int? TotalTokens { get; set; }
    }
}

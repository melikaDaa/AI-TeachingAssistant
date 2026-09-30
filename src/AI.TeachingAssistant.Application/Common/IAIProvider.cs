namespace AI.TeachingAssistant.Application.Common;

/// <summary>Request to the AI provider. Carries only text; no provider specifics.</summary>
public sealed record AiRequest(
    string SystemPrompt,
    string UserPrompt,
    int MaxTokens = 2000,
    double Temperature = 0.7);

/// <summary>Raw text response from the AI provider.</summary>
public sealed record AiResponse(string Content);

/// <summary>
/// Thrown when an AI call fails (timeout, network, rate limit, empty response, ...).
/// <see cref="IsTransient"/> marks failures worth one retry (timeout, network, 429, 5xx);
/// deterministic failures (missing key, auth) fail immediately.
/// </summary>
public class AiProviderException : Exception
{
    public bool IsTransient { get; }

    public AiProviderException(string message, Exception? inner = null, bool isTransient = false)
        : base(message, inner) => IsTransient = isTransient;
}

/// <summary>
/// Provider-agnostic AI abstraction. The application only ever talks to this interface, so the
/// concrete provider (mock, OpenAI-compatible, Gemini, Iranian gateways, ...) is replaceable.
/// Implementations must be thread-safe.
/// </summary>
public interface IAIProvider
{
    /// <summary>Human-friendly provider name for logs and diagnostics.</summary>
    string Name { get; }

    Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken cancellationToken = default);
}

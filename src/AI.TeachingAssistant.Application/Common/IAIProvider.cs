namespace AI.TeachingAssistant.Application.Common;

/// <summary>Request to the AI provider. Carries only text; no provider specifics.</summary>
public sealed record AiRequest(
    string SystemPrompt,
    string UserPrompt,
    int MaxTokens = 2000,
    double Temperature = 0.7);

/// <summary>Raw text response from the AI provider.</summary>
public sealed record AiResponse(string Content);

/// <summary>Thrown when an AI call fails (timeout, network, rate limit, empty response, ...).</summary>
public class AiProviderException : Exception
{
    public AiProviderException(string message, Exception? inner = null) : base(message, inner) { }
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

namespace AI.TeachingAssistant.Application.Common;

/// <summary>Request to render one educational image from a text prompt.</summary>
public sealed record ImageGenerationRequest(string Prompt, string Size = "1024x1024");

/// <summary>Rendered image bytes returned by an image provider.</summary>
public sealed record GeneratedImage(byte[] Data, string ContentType);

/// <summary>
/// Provider-agnostic image-generation abstraction, parallel to <see cref="IAIProvider"/>.
/// Kept separate from text generation so the frozen <see cref="IAIProvider"/> contract never widens.
/// Implementations must be thread-safe.
/// </summary>
public interface IImageProvider
{
    /// <summary>Human-friendly provider name for logs and diagnostics.</summary>
    string Name { get; }

    /// <summary>
    /// False when no usable provider is configured (offline/mock mode). Callers then skip
    /// rendering and keep the text prompts only, exactly like the pre-image behaviour.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>Upper bound on how many images one section may render (cost guard).</summary>
    int MaxImagesPerSection { get; }

    /// <summary>
    /// Renders one image. Throws <see cref="AiProviderException"/> on failure, with
    /// <see cref="AiProviderException.IsTransient"/> marking retryable availability errors.
    /// </summary>
    Task<GeneratedImage> GenerateAsync(ImageGenerationRequest request, CancellationToken cancellationToken = default);
}

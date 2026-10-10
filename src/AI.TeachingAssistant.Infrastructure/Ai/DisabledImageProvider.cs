using AI.TeachingAssistant.Application.Common;

namespace AI.TeachingAssistant.Infrastructure.Ai;

/// <summary>
/// Image provider used when none is configured (offline/mock mode, or missing key). It is
/// disabled, so the coordinator keeps the text image prompts and renders nothing — the
/// product still works end-to-end with no key and no cost.
/// </summary>
public sealed class DisabledImageProvider : IImageProvider
{
    public string Name => "Image generation disabled";

    public bool IsEnabled => false;

    public int MaxImagesPerSection => 0;

    public Task<GeneratedImage> GenerateAsync(ImageGenerationRequest request, CancellationToken cancellationToken = default) =>
        throw new AiProviderException("تولید تصویر در این نسخه فعال نیست.");
}

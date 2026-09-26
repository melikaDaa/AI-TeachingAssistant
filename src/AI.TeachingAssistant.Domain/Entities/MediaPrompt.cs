using AI.TeachingAssistant.Domain.Models;

namespace AI.TeachingAssistant.Domain.Entities;

/// <summary>Kind of AI media prompt stored in <see cref="MediaPrompt"/>.</summary>
public enum MediaPromptKind
{
    Image = 0,
    Video = 1,
}

/// <summary>
/// Sections E and F of the teaching package: image prompts (2 rows) and the video prompt (1 row).
/// </summary>
public class MediaPrompt : BaseEntity
{
    public int TeachingPackageId { get; set; }
    public TeachingPackage TeachingPackage { get; set; } = null!;

    public MediaPromptKind Kind { get; set; }

    /// <summary>Short title shown in the UI (e.g. «تصویر ۱ – نمودار سلول»).</summary>
    public string Title { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public ImagePromptModel? ImageModel => Kind == MediaPromptKind.Image ? PayloadJson.Deserialize<ImagePromptModel>() : null;
    public VideoPromptModel? VideoModel => Kind == MediaPromptKind.Video ? PayloadJson.Deserialize<VideoPromptModel>() : null;
}

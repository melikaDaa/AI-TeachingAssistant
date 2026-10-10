namespace AI.TeachingAssistant.Domain.Models;

/// <summary>Payload of one image prompt (stored as JSON). <see cref="FinalPrompt"/> is the copyable text.</summary>
public sealed class ImagePromptModel
{
    public string Subject { get; set; } = string.Empty;
    public string VisualElements { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public string Composition { get; set; } = string.Empty;
    public string EducationalPurpose { get; set; } = string.Empty;
    public string TextRestrictions { get; set; } = string.Empty;
    public string FinalPrompt { get; set; } = string.Empty;

    /// <summary>App-relative URL of the rendered image, or null when only the prompt exists.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>UTC timestamp of when the image was rendered, or null when only the prompt exists.</summary>
    public DateTime? ImageGeneratedAt { get; set; }
}

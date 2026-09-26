namespace AI.TeachingAssistant.Domain.Models;

/// <summary>Payload of the video prompt (stored as JSON). <see cref="FinalPrompt"/> is the copyable text.</summary>
public sealed class VideoPromptModel
{
    public string SceneDescription { get; set; } = string.Empty;
    public string Characters { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string Camera { get; set; } = string.Empty;
    public string Motion { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string EducationalObjective { get; set; } = string.Empty;
    public string VisualStyle { get; set; } = string.Empty;
    public string FinalPrompt { get; set; } = string.Empty;
}

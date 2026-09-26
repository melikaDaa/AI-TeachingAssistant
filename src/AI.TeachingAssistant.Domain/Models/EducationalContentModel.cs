namespace AI.TeachingAssistant.Domain.Models;

/// <summary>Payload of the educational content section (stored as JSON).</summary>
public sealed class EducationalContentModel
{
    public string SimpleExplanation { get; set; } = string.Empty;
    public List<string> KeyConcepts { get; set; } = new();
    public List<string> Examples { get; set; } = new();
    public List<string> CommonMisconceptions { get; set; } = new();
    public string RealWorldExample { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

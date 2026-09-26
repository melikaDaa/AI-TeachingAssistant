namespace AI.TeachingAssistant.Domain.Models;

/// <summary>Payload of the classroom activity section (stored as JSON).</summary>
public sealed class ActivityModel
{
    public string Title { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public List<string> Materials { get; set; } = new();
    public string StudentCount { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public List<string> Instructions { get; set; } = new();
    public string ExpectedResult { get; set; } = string.Empty;
}

namespace AI.TeachingAssistant.Domain.Models;

/// <summary>One timed segment of the lesson plan (e.g. 0–5 min: warm-up).</summary>
public sealed class LessonPlanSegment
{
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>Payload of the lesson plan section (stored as JSON).</summary>
public sealed class LessonPlanModel
{
    public List<string> LearningObjectives { get; set; } = new();
    public List<string> RequiredMaterials { get; set; } = new();
    public List<LessonPlanSegment> Segments { get; set; } = new();
    public string Homework { get; set; } = string.Empty;

    /// <summary>True when the segments cover the class duration with no gaps/overlaps.</summary>
    public bool HasRealisticSchedule(int durationMinutes)
    {
        if (Segments.Count == 0) return false;
        var expected = 0;
        foreach (var s in Segments)
        {
            if (s.StartMinute != expected) return false;
            if (s.EndMinute <= s.StartMinute) return false;
            expected = s.EndMinute;
        }
        return expected == durationMinutes;
    }
}

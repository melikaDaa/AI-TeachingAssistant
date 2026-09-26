using AI.TeachingAssistant.Domain.ValueObjects;

namespace AI.TeachingAssistant.Domain.Entities;

/// <summary>
/// Aggregate root for all AI-generated content of a lesson. Contains at most one section of each kind.
/// Regenerating a section replaces its child entity only.
/// </summary>
public class TeachingPackage : BaseEntity
{
    public int LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;

    public string[] CompletedSections { get; set; } = Array.Empty<string>();

    public LessonPlan? LessonPlan { get; set; }
    public EducationalContent? EducationalContent { get; set; }
    public ClassroomActivity? Activity { get; set; }
    public Quiz? Quiz { get; set; }

    /// <summary>Image and video prompts (Kind distinguishes them).</summary>
    public List<MediaPrompt> MediaPrompts { get; set; } = new();

    public GenerationProgress GetProgress() => new(
        Lesson.PackageStatus,
        0,
        null,
        CompletedSections);
}

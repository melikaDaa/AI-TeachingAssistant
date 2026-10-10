using AI.TeachingAssistant.Domain.ValueObjects;

namespace AI.TeachingAssistant.Domain.Entities;

/// <summary>A lesson created by the teacher with its generation status.</summary>
public class Lesson : BaseEntity
{
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string StudentLevel { get; set; } = string.Empty;
    public int? StudentCount { get; set; }
    public string? SessionStyle { get; set; }
    public string? AvailableResources { get; set; }
    public string? AdditionalInstructions { get; set; }

    public PackageStatus PackageStatus { get; set; } = PackageStatus.Draft;

    /// <summary>Persian error message of the last failed generation, shown to the teacher.</summary>
    public string? LastError { get; set; }

    /// <summary>Sections the teacher asked to generate (numeric codes of <see cref="ContentTypes"/>).</summary>
    public string RequestedSections { get; set; } = "1,2,3,4,5,6";

    public TeachingPackage? Package { get; set; }
}

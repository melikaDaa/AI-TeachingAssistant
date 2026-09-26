namespace AI.TeachingAssistant.Domain.Entities;

/// <summary>Lifecycle of the AI generation for a lesson's package.</summary>
public enum PackageStatus
{
    /// <summary>Lesson saved, no content generated yet.</summary>
    Draft = 0,

    /// <summary>AI generation is running in the background.</summary>
    Generating = 1,

    /// <summary>All requested sections were generated successfully.</summary>
    Ready = 2,

    /// <summary>Generation failed; a retry can be started.</summary>
    Failed = 3,
}

using AI.TeachingAssistant.Domain.Entities;

namespace AI.TeachingAssistant.Domain.ValueObjects;

/// <summary>Persisted generation status, read by the API while a generation job runs in the background.</summary>
public sealed record GenerationProgress(
    PackageStatus Status,
    int CurrentSection,
    string? LastError = null,
    string[]? CompletedSections = null)
{
    public static readonly GenerationProgress Empty = new(PackageStatus.Draft, 0);
}

namespace AI.TeachingAssistant.Application.Interfaces;

/// <summary>
/// Enqueues AI generation jobs. sectionType 0 means the full package;
/// otherwise a single ContentTypes code (independent per-section regeneration).
/// </summary>
public interface IGenerationJobQueue
{
    Task<string> EnqueueAsync(int lessonId, int teacherId, int sectionType, string requestKey, CancellationToken ct = default);
}

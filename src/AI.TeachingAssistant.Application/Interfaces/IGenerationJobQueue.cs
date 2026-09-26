namespace AI.TeachingAssistant.Application.Interfaces;

/// <summary>
/// Enqueues AI generation jobs. sectionType 0 means the full package;
/// otherwise a single ContentTypes code (independent per-section regeneration).
/// </summary>
public interface IGenerationJobQueue
{
    void Enqueue(int lessonId, int sectionType);
}

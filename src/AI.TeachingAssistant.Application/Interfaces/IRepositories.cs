using AI.TeachingAssistant.Domain.Entities;

namespace AI.TeachingAssistant.Application.Interfaces;

public interface ITeacherRepository
{
    Task<Teacher?> GetDefaultAsync(CancellationToken ct = default);
    Task<Teacher> UpdateAsync(Teacher teacher, CancellationToken ct = default);
}

public interface ILessonRepository
{
    Task<Lesson?> GetByIdAsync(int id, bool includePackage = false, CancellationToken ct = default);
    Task<List<Lesson>> ListForTeacherAsync(int teacherId, CancellationToken ct = default);
    Task<Lesson> AddAsync(Lesson lesson, CancellationToken ct = default);
    Task UpdateAsync(Lesson lesson, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public interface ITeachingPackageRepository
{
    Task<TeachingPackage?> GetForLessonAsync(int lessonId, CancellationToken ct = default);
    Task<TeachingPackage> AddAsync(TeachingPackage package, CancellationToken ct = default);
    Task UpdateAsync(TeachingPackage package, CancellationToken ct = default);
}

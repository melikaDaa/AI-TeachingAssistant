using AI.TeachingAssistant.Application.Interfaces;
using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AI.TeachingAssistant.Infrastructure.Repositories;

public class TeacherRepository : ITeacherRepository
{
    private readonly AppDbContext _db;
    public TeacherRepository(AppDbContext db) => _db = db;

    public async Task<Teacher?> GetDefaultAsync(CancellationToken ct = default) =>
        await _db.Teachers.OrderBy(t => t.Id).FirstOrDefaultAsync(ct);

    public async Task<Teacher> UpdateAsync(Teacher teacher, CancellationToken ct = default)
    {
        teacher.UpdatedAt = DateTime.UtcNow;
        _db.Teachers.Update(teacher);
        await _db.SaveChangesAsync(ct);
        return teacher;
    }
}

public class LessonRepository : ILessonRepository
{
    private readonly AppDbContext _db;
    public LessonRepository(AppDbContext db) => _db = db;

    public async Task<Lesson?> GetByIdAsync(int id, bool includePackage = false, CancellationToken ct = default)
    {
        var query = _db.Lessons.AsQueryable();
        if (includePackage)
        {
            query = query
                .Include(l => l.Package).ThenInclude(p => p!.LessonPlan)
                .Include(l => l.Package).ThenInclude(p => p!.EducationalContent)
                .Include(l => l.Package).ThenInclude(p => p!.Activity)
                .Include(l => l.Package).ThenInclude(p => p!.Quiz)
                .Include(l => l.Package).ThenInclude(p => p!.MediaPrompts);
        }
        return await query.FirstOrDefaultAsync(l => l.Id == id, ct);
    }

    public async Task<List<Lesson>> ListForTeacherAsync(int teacherId, CancellationToken ct = default) =>
        await _db.Lessons
            .Where(l => l.TeacherId == teacherId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

    public async Task<Lesson> AddAsync(Lesson lesson, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        lesson.CreatedAt = now;
        lesson.UpdatedAt = now;
        _db.Lessons.Add(lesson);
        await _db.SaveChangesAsync(ct);
        return lesson;
    }

    public async Task UpdateAsync(Lesson lesson, CancellationToken ct = default)
    {
        lesson.UpdatedAt = DateTime.UtcNow;
        _db.Lessons.Update(lesson);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var affected = await _db.Lessons.Where(l => l.Id == id).ExecuteDeleteAsync(ct);
        return affected > 0;
    }
}

public class TeachingPackageRepository : ITeachingPackageRepository
{
    private readonly AppDbContext _db;
    public TeachingPackageRepository(AppDbContext db) => _db = db;

    public async Task<TeachingPackage?> GetForLessonAsync(int lessonId, CancellationToken ct = default) =>
        await _db.TeachingPackages
            .Include(p => p.LessonPlan)
            .Include(p => p.EducationalContent)
            .Include(p => p.Activity)
            .Include(p => p.Quiz)
            .Include(p => p.MediaPrompts)
            .FirstOrDefaultAsync(p => p.LessonId == lessonId, ct);

    public async Task<TeachingPackage> AddAsync(TeachingPackage package, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        package.CreatedAt = now;
        package.UpdatedAt = now;
        _db.TeachingPackages.Add(package);
        await _db.SaveChangesAsync(ct);
        return package;
    }

    public async Task UpdateAsync(TeachingPackage package, CancellationToken ct = default)
    {
        package.UpdatedAt = DateTime.UtcNow;
        _db.TeachingPackages.Update(package);
        await _db.SaveChangesAsync(ct);
    }
}

using AI.TeachingAssistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AI.TeachingAssistant.Infrastructure.Persistence;

/// <summary>Applies migrations and seeds the demo teacher and sample lessons (spec §28).</summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        if (!await db.Teachers.AnyAsync(ct))
        {
            var now = DateTime.UtcNow;
            db.Teachers.Add(new Teacher
            {
                FullName = "مریم احمدی",
                Level = TeachingLevels.MiddleSchool,
                Grade = "هفتم",
                Subjects = "علوم",
                TeachingStyle = "تعاملی",
                CreatedAt = now,
                UpdatedAt = now,
            });
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Lessons.AnyAsync(ct))
        {
            var now = DateTime.UtcNow;
            var teacher = await db.Teachers.FirstAsync(ct);
            db.Lessons.AddRange(
                new Lesson
                {
                    TeacherId = teacher.Id,
                    Title = "سلول چیست؟",
                    Subject = "علوم",
                    Grade = "هفتم",
                    Topic = "سلول",
                    DurationMinutes = 60,
                    StudentLevel = "متوسط",
                    AdditionalInstructions = "درس را تعاملی و مناسب گفت‌وگوی کلاسی طراحی کن.",
                    RequestedSections = "1,2,3,4,5,6",
                    PackageStatus = PackageStatus.Draft,
                    CreatedAt = now,
                    UpdatedAt = now,
                },
                new Lesson
                {
                    TeacherId = teacher.Id,
                    Title = "زنجیره غذایی",
                    Subject = "علوم",
                    Grade = "هفتم",
                    Topic = "زنجیره غذایی",
                    DurationMinutes = 45,
                    StudentLevel = "مبتدی",
                    RequestedSections = "1,2,4",
                    PackageStatus = PackageStatus.Draft,
                    CreatedAt = now.AddHours(-3),
                    UpdatedAt = now.AddHours(-3),
                });
            await db.SaveChangesAsync(ct);
        }
    }
}

using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Application.Services;
using AI.TeachingAssistant.Infrastructure;
using AI.TeachingAssistant.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AI.TeachingAssistant.Tests;

public sealed class UsageServiceTests
{
    [Fact]
    public async Task Concurrent_reservations_cannot_exceed_sqlite_quota()
    {
        var file = Path.Combine(Path.GetTempPath(), $"usage-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={file};Default Timeout=2").Options;
            await using (var db = new AppDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                db.Teachers.Add(new AI.TeachingAssistant.Domain.Entities.Teacher { FullName = "A", Level = "x", Grade = "1", Subjects = "x", TeachingStyle = "x" });
                await db.SaveChangesAsync();
            }
            int teacherId;
            await using (var db = new AppDbContext(options)) teacherId = await db.Teachers.Select(x => x.Id).SingleAsync();
            var optionsUsage = new UsageOptions { DailyTextUnits = 3, DailyImageUnits = 2, ShortWindowRequests = 100 };
            var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(async i =>
            {
                await using var db = new AppDbContext(options);
                return await new UsageService(db, optionsUsage).AdmitAsync(teacherId, ["lesson-plan"], $"concurrent-{i}");
            }));
            Assert.Equal(3, results.Count(x => x));
        }
        finally
        {
            for (var attempt = 0; attempt < 5 && File.Exists(file); attempt++)
            {
                try { File.Delete(file); } catch (IOException) { await Task.Delay(50); }
            }
        }
    }

    [Fact]
    public async Task Quota_is_separate_per_teacher_and_reservation_is_confirmed_only_on_success()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var a = new AI.TeachingAssistant.Domain.Entities.Teacher { FullName = "A", Level = "x", Grade = "1", Subjects = "x", TeachingStyle = "x" };
        var b = new AI.TeachingAssistant.Domain.Entities.Teacher { FullName = "B", Level = "x", Grade = "1", Subjects = "x", TeachingStyle = "x" };
        db.Teachers.AddRange(a, b); await db.SaveChangesAsync();
        var service = new UsageService(db, new UsageOptions { DailyTextUnits = 3, DailyImageUnits = 2 });

        Assert.True(await service.ReserveAsync(a.Id, ["lesson-plan"], "key1"));
        await service.CompleteReservationAsync(a.Id, "lesson-plan", "key1:lesson-plan", true);
        Assert.Equal(1, (await service.GetAsync(a.Id)).TextUsed);
        Assert.Equal(0, (await service.GetAsync(b.Id)).TextUsed);

        Assert.True(await service.ReserveAsync(a.Id, ["content"], "key2"));
        await service.CompleteReservationAsync(a.Id, "content", "key2:content", false);
        Assert.Equal(1, (await service.GetAsync(a.Id)).TextUsed);
    }

    [Fact]
    public async Task Reconciliation_releases_only_expired_orphan_reservations_and_is_idempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var optionsDb = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(optionsDb); await db.Database.EnsureCreatedAsync();
        var teacher = new AI.TeachingAssistant.Domain.Entities.Teacher { FullName = "A", Level = "x", Grade = "1", Subjects = "x", TeachingStyle = "x" };
        db.Teachers.Add(teacher); await db.SaveChangesAsync();
        db.UsageEvents.AddRange(
            new AI.TeachingAssistant.Domain.Entities.UsageEvent { TeacherId = teacher.Id, Operation = "lesson-plan", RequestKey = "live:lesson-plan", ParentRequestKey = "live", IsReservation = true, LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(-2), CreatedAtUtc = DateTime.UtcNow.AddMinutes(-3) },
            new AI.TeachingAssistant.Domain.Entities.UsageEvent { TeacherId = teacher.Id, Operation = "content", RequestKey = "orphan:content", ParentRequestKey = "orphan", IsReservation = true, LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(-2), CreatedAtUtc = DateTime.UtcNow.AddMinutes(-3) });
        await db.SaveChangesAsync();
        var service = new UsageService(db, new UsageOptions { ReservationLeaseMinutes = 30 });
        await service.ReconcileOrphanedReservationsAsync(new HashSet<string> { "live" });
        await service.ReconcileOrphanedReservationsAsync(new HashSet<string> { "live" });
        Assert.True(await db.UsageEvents.AnyAsync(x => x.ParentRequestKey == "live" && x.IsReservation));
        Assert.False(await db.UsageEvents.AnyAsync(x => x.ParentRequestKey == "orphan" && x.IsReservation));
    }

    [Fact]
    public async Task Image_reservations_are_independent_and_idempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var optionsDb = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(optionsDb); await db.Database.EnsureCreatedAsync();
        var teacher = new AI.TeachingAssistant.Domain.Entities.Teacher { FullName = "A", Level = "x", Grade = "1", Subjects = "x", TeachingStyle = "x" };
        db.Teachers.Add(teacher); await db.SaveChangesAsync();
        var usage = new UsageService(db, new UsageOptions { DailyTextUnits = 10, DailyImageUnits = 3, ShortWindowRequests = 10 });

        Assert.True(await usage.AdmitAsync(teacher.Id, ["image-prompt", "image"], "images-job", imageReservations: 3));
        var first = UsageReservationKeys.Image("images-job", 0);
        var second = UsageReservationKeys.Image("images-job", 1);
        var third = UsageReservationKeys.Image("images-job", 2);
        await usage.CompleteReservationAsync(teacher.Id, "image", first, true, units: 1);
        await usage.CompleteReservationAsync(teacher.Id, "image", first, true, units: 1);
        await usage.CompleteReservationAsync(teacher.Id, "image", second, false);
        Assert.Equal(1, (await usage.GetAsync(teacher.Id)).ImageUsed);
        Assert.True(await db.UsageEvents.AnyAsync(x => x.RequestKey == third && x.IsReservation));
        await usage.ReleaseReservationsAsync("images-job");
        await usage.ReleaseReservationsAsync("images-job");
        Assert.Equal(1, (await usage.GetAsync(teacher.Id)).ImageUsed);
        Assert.False(await db.UsageEvents.AnyAsync(x => x.ParentRequestKey == "images-job" && x.IsReservation));
    }
}

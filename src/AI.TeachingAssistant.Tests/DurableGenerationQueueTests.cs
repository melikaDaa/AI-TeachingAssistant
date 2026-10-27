using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Infrastructure.Generation;
using AI.TeachingAssistant.Infrastructure.Persistence;
using AI.TeachingAssistant.Infrastructure.Repositories;
using AI.TeachingAssistant.Infrastructure.Ai;
using AI.TeachingAssistant.Application.Interfaces;
using AI.TeachingAssistant.Application.Services;
using AI.TeachingAssistant.Application.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI.TeachingAssistant.Tests;

public sealed class DurableGenerationQueueTests
{
    [Fact]
    public async Task Enqueued_job_survives_new_queue_scope_and_is_idempotent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"durable-{Guid.NewGuid():N}.db");
        var cs = $"Data Source={path};Default Timeout=3";
        try
        {
            await using (var db = NewDb(cs))
            {
                await AI.TeachingAssistant.Infrastructure.Persistence.DatabaseInitializer.InitializeAsync(db);
                var teacher = AddTeacher(db); await db.SaveChangesAsync();
                var queue = BuildQueue(cs);
                await queue.EnqueueAsync(10, teacher.Id, ContentTypes.LessonPlan, "stable-job");
                await queue.EnqueueAsync(10, teacher.Id, ContentTypes.LessonPlan, "stable-job");
            }
            await using var restarted = NewDb(cs);
            Assert.Single(await restarted.GenerationJobs.Where(x => x.RequestKey == "stable-job" && x.Status == GenerationJobStatus.Pending).ToListAsync());
        }
        finally { try { File.Delete(path); } catch (IOException) { } }
    }

    [Fact]
    public async Task Concurrent_workers_can_claim_an_expired_job_with_only_one_current_fence()
    {
        var path = Path.Combine(Path.GetTempPath(), $"claim-{Guid.NewGuid():N}.db");
        var cs = $"Data Source={path};Default Timeout=3";
        try
        {
            await using (var db = NewDb(cs))
            {
                await AI.TeachingAssistant.Infrastructure.Persistence.DatabaseInitializer.InitializeAsync(db);
                var teacher = AddTeacher(db); await db.SaveChangesAsync();
                db.GenerationJobs.Add(new GenerationJobRecord { RequestKey = "claim-test", LessonId = 1, TeacherId = teacher.Id,
                    SectionType = ContentTypes.LessonPlan, Status = GenerationJobStatus.Pending, CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1), UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-1) });
                await db.SaveChangesAsync();
            }
            using var providerOne = BuildWorkerProvider(cs, 1);
            using var providerTwo = BuildWorkerProvider(cs, 1);
            var optionsOne = new UsageOptions { WorkerId = "race", ReservationLeaseMinutes = 2, LeaseHeartbeatSeconds = 1 };
            var optionsTwo = new UsageOptions { WorkerId = "race", ReservationLeaseMinutes = 2, LeaseHeartbeatSeconds = 1 };
            var workerOne = new GenerationJobWorker(providerOne.GetRequiredService<IServiceScopeFactory>(), NullLogger<GenerationJobWorker>.Instance, optionsOne);
            var workerTwo = new GenerationJobWorker(providerTwo.GetRequiredService<IServiceScopeFactory>(), NullLogger<GenerationJobWorker>.Instance, optionsTwo);
            var claims = await Task.WhenAll(workerOne.ClaimAsync(CancellationToken.None), workerTwo.ClaimAsync(CancellationToken.None));
            Assert.Equal(1, claims.Count(x => x is not null));
            await using var verify = NewDb(cs);
            var job = await verify.GenerationJobs.SingleAsync(x => x.RequestKey == "claim-test");
            Assert.Equal(GenerationJobStatus.Processing, job.Status);
            Assert.Equal(1, job.FencingToken);
            var winner = claims.Single(x => x is not null)!;
            var staleWorker = winner.OwnerId == workerOne.WorkerId ? workerTwo : workerOne;
            var priorFence = winner.FencingToken;
            await verify.GenerationJobs.Where(x => x.Id == job.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseExpiresAtUtc, DateTime.UtcNow.AddSeconds(-1)));
            var next = await staleWorker.ClaimAsync(CancellationToken.None);
            Assert.NotNull(next);
            Assert.Equal(priorFence + 1, next!.FencingToken);
            Assert.False(await (winner.OwnerId == workerOne.WorkerId ? workerOne : workerTwo).OwnsLeaseAsync(job.Id, priorFence, CancellationToken.None));
            Assert.False(await (winner.OwnerId == workerOne.WorkerId ? workerOne : workerTwo).RenewLeaseAsync(job.Id, priorFence, CancellationToken.None));
        }
        finally { try { File.Delete(path); } catch (IOException) { } }
    }

    [Fact]
    public async Task New_worker_processes_pending_job_after_restart_and_usage_is_not_duplicated()
    {
        var path = Path.Combine(Path.GetTempPath(), $"restart-{Guid.NewGuid():N}.db");
        var cs = $"Data Source={path};Default Timeout=5";
        try
        {
            int lessonId, teacherId;
            await using (var initial = NewDb(cs))
            {
                await AI.TeachingAssistant.Infrastructure.Persistence.DatabaseInitializer.InitializeAsync(initial);
                var teacher = AddTeacher(initial); await initial.SaveChangesAsync(); teacherId = teacher.Id;
                var lesson = new AI.TeachingAssistant.Domain.Entities.Lesson { TeacherId = teacherId, Title = "T", Subject = "S", Grade = "1", Topic = "سلول", DurationMinutes = 45,
                    StudentLevel = "متوسط", RequestedSections = ContentTypes.LessonPlan.ToString(), PackageStatus = AI.TeachingAssistant.Domain.Entities.PackageStatus.Generating };
                initial.Lessons.Add(lesson); await initial.SaveChangesAsync(); lessonId = lesson.Id;
                var usage = new AI.TeachingAssistant.Application.Services.UsageService(initial, new UsageOptions { DailyTextUnits = 20, DailyImageUnits = 5, ShortWindowRequests = 20 });
                Assert.True(await usage.AdmitAsync(teacherId, ["lesson-plan"], "restart-job"));
                initial.GenerationJobs.Add(new GenerationJobRecord { RequestKey = "restart-job", LessonId = lessonId, TeacherId = teacherId, SectionType = ContentTypes.LessonPlan,
                    Status = GenerationJobStatus.Pending, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
                await initial.SaveChangesAsync();
            }

            // The first provider/scope is disposed before the replacement worker is built.
            await using (var oldScopeMarker = NewDb(cs)) { Assert.Equal(GenerationJobStatus.Pending, (await oldScopeMarker.GenerationJobs.SingleAsync()).Status); }
            using var restartedProvider = BuildWorkerProvider(cs, teacherId);
            var options = new UsageOptions { WorkerId = "restart-test", ReservationLeaseMinutes = 2, LeaseHeartbeatSeconds = 1 };
            var worker = new GenerationJobWorker(restartedProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<GenerationJobWorker>.Instance, options);
            Assert.True(await worker.ProcessOneAsync());
            Assert.False(await worker.ProcessOneAsync());

            await using var verify = NewDb(cs);
            var completed = await verify.GenerationJobs.SingleAsync(x => x.RequestKey == "restart-job");
            Assert.Equal(GenerationJobStatus.Succeeded, completed.Status);
            var lessonAfter = await verify.Lessons.Include(x => x.Package).ThenInclude(x => x!.LessonPlan).SingleAsync(x => x.Id == lessonId);
            Assert.NotNull(lessonAfter.Package!.LessonPlan);
            var eventRow = await verify.UsageEvents.SingleAsync(x => x.RequestKey == "restart-job:lesson-plan");
            Assert.False(eventRow.IsReservation);
            Assert.True(eventRow.Succeeded);
            Assert.Equal(1, await verify.UsageEvents.CountAsync(x => x.RequestKey == "restart-job:lesson-plan"));
        }
        finally { try { File.Delete(path); } catch (IOException) { } }
    }

    private static ServiceProvider BuildWorkerProvider(string cs, int teacherId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(cs));
        services.AddSingleton<UsageOptions>(new UsageOptions { DailyTextUnits = 20, DailyImageUnits = 5 });
        services.AddScoped<IUsageService, AI.TeachingAssistant.Application.Services.UsageService>();
        services.AddScoped<ICurrentTeacherContext>(_ => new FixedTeacherContext(teacherId));
        services.AddScoped<ICurrentTeacher>(sp => sp.GetRequiredService<ICurrentTeacherContext>());
        services.AddScoped<ILessonRepository, LessonRepository>();
        services.AddScoped<ITeachingPackageRepository, TeachingPackageRepository>();
        services.AddScoped<ITeacherRepository, TeacherRepository>();
        services.AddScoped<IAIProvider, MockAIProvider>();
        services.AddScoped<SectionGenerationService>();
        services.AddScoped<GenerationCoordinator>();
        services.AddSingleton<IImageProvider, DisabledImageProvider>();
        services.AddSingleton<IImageStore>(new InMemoryImageStore());
        return services.BuildServiceProvider();
    }

    private sealed class FixedTeacherContext(int teacherId) : ICurrentTeacherContext
    {
        public int TeacherId => teacherId;
        public void Set(int id) { }
    }

    private static GenerationJobQueue BuildQueue(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString));
        var provider = services.BuildServiceProvider();
        return new GenerationJobQueue(provider.GetRequiredService<IServiceScopeFactory>());
    }

    private static AppDbContext NewDb(string cs) => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(cs).Options);
    private static Teacher AddTeacher(AppDbContext db)
    {
        var teacher = new Teacher { FullName = "Test", Level = "x", Grade = "1", Subjects = "x", TeachingStyle = "x" };
        db.Teachers.Add(teacher); return teacher;
    }
}

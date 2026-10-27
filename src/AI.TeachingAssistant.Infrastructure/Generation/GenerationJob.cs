using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Application.Interfaces;
using AI.TeachingAssistant.Application.Services;
using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AI.TeachingAssistant.Infrastructure.Generation;

/// <summary>Durable SQLite-backed job repository. Polling is intentionally simple for the single-file MVP.</summary>
public sealed class GenerationJobQueue(IServiceScopeFactory scopeFactory) : IGenerationJobQueue
{
    public async Task<string> EnqueueAsync(int lessonId, int teacherId, int sectionType, string requestKey, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var existing = await db.GenerationJobs.SingleOrDefaultAsync(x => x.RequestKey == requestKey, ct);
        if (existing is not null)
        {
            if (existing.LessonId != lessonId || existing.TeacherId != teacherId || existing.SectionType != sectionType)
                throw new InvalidOperationException("Request key already belongs to a different generation request.");
            return requestKey;
        }
        var now = DateTime.UtcNow;
        db.GenerationJobs.Add(new GenerationJobRecord { RequestKey = requestKey, LessonId = lessonId, TeacherId = teacherId,
            SectionType = sectionType, Status = GenerationJobStatus.Pending, CreatedAtUtc = now, UpdatedAtUtc = now });
        await db.SaveChangesAsync(ct);
        return requestKey;
    }
}

/// <summary>Claims durable work with an atomic conditional update and fencing token.</summary>
public sealed class GenerationJobWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GenerationJobWorker> _logger;
    private readonly UsageOptions _options;
    private readonly string _workerId;
    public string WorkerId => _workerId;

    public GenerationJobWorker(IServiceScopeFactory scopeFactory, ILogger<GenerationJobWorker> logger, UsageOptions options)
    {
        _scopeFactory = scopeFactory; _logger = logger; _options = options;
        _workerId = $"{options.WorkerId}:{Guid.NewGuid():N}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await ClaimAsync(stoppingToken);
                if (job is null)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
                    continue;
                }
                await ProcessAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Durable generation worker loop failed");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }

    public async Task<GenerationJobRecord?> ClaimAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) != true)
            throw new NotSupportedException("Durable job claiming currently requires SQLite.");

        await db.Database.OpenConnectionAsync(ct);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        await using var sqliteTransaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
        await db.Database.UseTransactionAsync(sqliteTransaction, ct);
        try
        {
            var now = DateTime.UtcNow;
            var candidate = await db.GenerationJobs.AsNoTracking()
                .Where(x => x.Status == GenerationJobStatus.Pending || (x.Status == GenerationJobStatus.Processing && x.LeaseExpiresAtUtc < now))
                .OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
            if (candidate is null)
            {
                await sqliteTransaction.CommitAsync(ct);
                return null;
            }

            var wasExpiredProcessing = candidate.Status == GenerationJobStatus.Processing;

            var changed = await db.GenerationJobs.Where(x => x.Id == candidate.Id &&
                    (x.Status == GenerationJobStatus.Pending || (x.Status == GenerationJobStatus.Processing && x.LeaseExpiresAtUtc < now)))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, GenerationJobStatus.Processing)
                    .SetProperty(x => x.OwnerId, _workerId).SetProperty(x => x.FencingToken, x => x.FencingToken + 1)
                    .SetProperty(x => x.LeaseExpiresAtUtc, now.AddMinutes(Math.Max(1, _options.ReservationLeaseMinutes)))
                    .SetProperty(x => x.UpdatedAtUtc, now), ct);
            if (changed != 1) throw new InvalidOperationException("Job claim lost despite SQLite write transaction.");
            await sqliteTransaction.CommitAsync(ct);
            await db.Database.CloseConnectionAsync();
            using var readScope = _scopeFactory.CreateScope();
            var readDb = readScope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await readDb.GenerationJobs.AsNoTracking().SingleAsync(x => x.Id == candidate.Id, ct);
        }
        catch
        {
            try { await sqliteTransaction.RollbackAsync(CancellationToken.None); } catch { }
            throw;
        }
    }

    public async Task<bool> ProcessOneAsync(CancellationToken ct = default)
    {
        var job = await ClaimAsync(ct);
        if (job is null) return false;
        await ProcessAsync(job, ct);
        return true;
    }


    private async Task ProcessAsync(GenerationJobRecord job, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var usage = scope.ServiceProvider.GetRequiredService<IUsageService>();
        using var heartbeatStop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var operationStop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task? heartbeatTask = null;
        try
        {
            scope.ServiceProvider.GetRequiredService<ICurrentTeacherContext>().Set(job.TeacherId);
            var coordinator = scope.ServiceProvider.GetRequiredService<GenerationCoordinator>();
            coordinator.UsageRequestKey = job.RequestKey;
            coordinator.JobOwnerId = _workerId;
            coordinator.JobFencingToken = job.FencingToken;
            coordinator.JobId = job.Id;
            coordinator.Heartbeat = token => RenewLeaseAsync(job.Id, job.FencingToken, token);
            coordinator.CanCommit = token => OwnsLeaseAsync(job.Id, job.FencingToken, token);
            coordinator.RequireOwnership = token => RequireOwnershipAsync(job, token);
            coordinator.IsCurrentOwner = token => OwnsLeaseAsync(job.Id, job.FencingToken, token);

            heartbeatTask = RunHeartbeatAsync(job, heartbeatStop.Token, operationStop);

            var result = job.SectionType == ContentTypes.All
                ? await coordinator.GeneratePackageAsync(job.LessonId, operationStop.Token)
                : await coordinator.GenerateSectionAsync(job.LessonId, job.SectionType, operationStop.Token);

            heartbeatStop.Cancel();
            await heartbeatTask;
            heartbeatTask = null;

            if (result.Ok) await SettleAsync(db, usage, job, GenerationJobStatus.Succeeded, null, ct);
            else await SettleAsync(db, usage, job, GenerationJobStatus.Failed, "generation_failed", ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown is controlled: release ownership and remaining reservations; hard crashes are recovered by lease expiry.
            try { await SettleAsync(db, usage, job, GenerationJobStatus.Pending, "worker_shutdown", CancellationToken.None); }
            catch (Exception settleEx) { _logger.LogError(settleEx, "Could not release job {JobId} during shutdown", job.Id); }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generation job {JobId} failed", job.Id);
            try { await SettleAsync(db, usage, job, GenerationJobStatus.Failed, "worker_failure", CancellationToken.None); }
            catch (Exception settleEx) { _logger.LogError(settleEx, "Failed to settle generation job {JobId}", job.Id); }
        }
        finally
        {
            heartbeatStop.Cancel();
            if (heartbeatTask is not null)
                try { await heartbeatTask; } catch (Exception ex) { _logger.LogWarning(ex, "Heartbeat task stopped for job {JobId}", job.Id); }
        }
    }

    private async Task RunHeartbeatAsync(GenerationJobRecord job, CancellationToken ct, CancellationTokenSource operationStop)
    {
        var interval = TimeSpan.FromSeconds(Math.Clamp(_options.LeaseHeartbeatSeconds, 1, Math.Max(1, _options.ReservationLeaseMinutes * 60 / 3)));
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (!await RenewLeaseAsync(job.Id, job.FencingToken, ct))
                {
                    _logger.LogWarning("Lease renewal rejected for job {JobId}, fence {Fence}; cancelling local work", job.Id, job.FencingToken);
                    operationStop.Cancel();
                    throw new InvalidOperationException($"Lease renewal rejected for job {job.Id}, fence {job.FencingToken}.");
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    public async Task<bool> RenewLeaseAsync(long jobId, long token, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        return await db.GenerationJobs.Where(x => x.Id == jobId && x.Status == GenerationJobStatus.Processing
                && x.OwnerId == _workerId && x.FencingToken == token)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseExpiresAtUtc, now.AddMinutes(Math.Max(1, _options.ReservationLeaseMinutes)))
                .SetProperty(x => x.UpdatedAtUtc, now), ct) == 1;
    }

    public async Task<bool> OwnsLeaseAsync(long jobId, long token, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.GenerationJobs.AnyAsync(x => x.Id == jobId && x.Status == GenerationJobStatus.Processing
            && x.OwnerId == _workerId && x.FencingToken == token && x.LeaseExpiresAtUtc > DateTime.UtcNow, ct);
    }

    private async Task RequireOwnershipAsync(GenerationJobRecord job, CancellationToken ct)
    {
        if (!await OwnsLeaseAsync(job.Id, job.FencingToken, ct))
            throw new InvalidOperationException("Worker lease is no longer current.");
    }

    private async Task SettleAsync(AppDbContext db, IUsageService usage, GenerationJobRecord job,
        GenerationJobStatus status, string? failure, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var changed = await db.GenerationJobs.Where(x => x.Id == job.Id && x.Status == GenerationJobStatus.Processing
                && x.OwnerId == _workerId && x.FencingToken == job.FencingToken)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status).SetProperty(x => x.FailureCode, failure)
                .SetProperty(x => x.OwnerId, (string?)null).SetProperty(x => x.LeaseExpiresAtUtc, (DateTime?)null)
                .SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow), ct);
        if (changed == 0) throw new InvalidOperationException("Worker no longer owns the job; stale result was fenced out.");
        if (status is GenerationJobStatus.Failed or GenerationJobStatus.Pending)
            await SettleUsageInTransactionAsync(db, job, ct);
        await tx.CommitAsync(ct);
    }

    private static async Task SettleUsageInTransactionAsync(AppDbContext db, GenerationJobRecord job, CancellationToken ct)
    {
        var reservations = await db.UsageEvents.Where(x => (x.ParentRequestKey == job.RequestKey || x.RequestKey == job.RequestKey) && x.IsReservation).ToListAsync(ct);
        foreach (var reservation in reservations)
        {
            // Remaining reservations mean the corresponding section did not reach its own successful finalization.
            db.UsageEvents.Remove(reservation);
        }
        await db.SaveChangesAsync(ct);
    }
}

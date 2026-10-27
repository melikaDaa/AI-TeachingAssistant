using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Application.Interfaces;
using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace AI.TeachingAssistant.Application.Services;

public sealed class UsageService(AppDbContext db, UsageOptions options) : IUsageService
{
    public async Task<UsageSnapshot> GetAsync(int teacherId, CancellationToken ct = default)
    {
        var start = ResetStart(DateTime.UtcNow);
        var rows = await db.UsageEvents.AsNoTracking().Where(x => x.TeacherId == teacherId && x.Succeeded && !x.IsReservation && x.CreatedAtUtc >= start).ToListAsync(ct);
        return new UsageSnapshot(options.DailyTextUnits, rows.Where(x => x.Operation != "image").Sum(x => x.Units),
            options.DailyImageUnits, rows.Where(x => x.Operation == "image").Sum(x => x.Units), start.AddDays(1), options.WarningPercent,
            rows.GroupBy(x => x.Operation).ToDictionary(g => g.Key, g => g.Sum(x => x.Units)));
    }

    public async Task<bool> ReserveAsync(int teacherId, IReadOnlyCollection<string> operations, string requestKey, CancellationToken ct = default)
    {
        if (!options.Enabled) return true;
        if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) != true)
            throw new NotSupportedException("Atomic usage reservation currently requires SQLite BEGIN IMMEDIATE.");
        return await InImmediateTransactionAsync(async () =>
        {
            if (await db.UsageEvents.AnyAsync(x => x.ParentRequestKey == requestKey, ct)) return false;
            if (!await CanReserveAsync(teacherId, operations, ct)) return false;
            var now = DateTime.UtcNow;
            foreach (var operation in operations.Where(x => x != "image"))
                db.UsageEvents.Add(new UsageEvent { TeacherId = teacherId, Operation = operation, RequestKey = requestKey + ":" + operation,
                    ParentRequestKey = requestKey, Units = OperationUnits(operation),
                    IsReservation = true, Succeeded = false, CreatedAtUtc = now,
                    LeaseExpiresAtUtc = now.AddMinutes(Math.Max(1, options.ReservationLeaseMinutes)) });
            if (operations.Contains("image"))
                db.UsageEvents.Add(new UsageEvent { TeacherId = teacherId, Operation = "image", RequestKey = UsageReservationKeys.Image(requestKey, 0),
                    ParentRequestKey = requestKey, Units = options.ImageUnits, IsReservation = true, Succeeded = false, CreatedAtUtc = now,
                    LeaseExpiresAtUtc = now.AddMinutes(Math.Max(1, options.ReservationLeaseMinutes)) });
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    public async Task<bool> AdmitAsync(int teacherId, IReadOnlyCollection<string> operations, string requestKey, CancellationToken ct = default, int imageReservations = 1)
    {
        if (!options.Enabled) return true;
        if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) != true)
            throw new NotSupportedException("Atomic usage admission currently requires SQLite BEGIN IMMEDIATE.");
        return await InImmediateTransactionAsync(async () =>
        {
            var now = DateTime.UtcNow;
            var window = Math.Max(1, options.ShortWindowMinutes);
            if (await db.UsageEvents.AnyAsync(x => x.ParentRequestKey == requestKey, ct))
            {
                var existing = await db.UsageEvents.Where(x => x.ParentRequestKey == requestKey).ToListAsync(ct);
                var expected = operations.Where(x => x != "image").ToList();
                if (operations.Contains("image-prompt") && operations.Contains("image")) expected.AddRange(Enumerable.Repeat("image", Math.Clamp(imageReservations, 1, 20)));
                return existing.All(x => x.TeacherId == teacherId)
                    && existing.Where(x => x.Operation != "request").Select(x => x.Operation).Order().SequenceEqual(expected.Order());
            }
            var count = await db.UsageEvents.CountAsync(x => x.TeacherId == teacherId && x.Operation == "request"
                && x.CreatedAtUtc >= now.AddMinutes(-window) && x.IsRateLimitedRequest, ct);
            if (count >= Math.Max(1, options.ShortWindowRequests)) return false;
            if (!await CanReserveAsync(teacherId, operations, ct, imageReservations)) return false;
            var unitsToReserve = new List<(string Operation, string Key, int Units)>();
            foreach (var operation in operations)
            {
                if (operation == "image") continue;
                unitsToReserve.Add((operation, requestKey + ":" + operation, OperationUnits(operation)));
            }
            if (operations.Contains("image-prompt") && operations.Contains("image"))
                for (var i = 0; i < Math.Clamp(imageReservations, 1, 20); i++)
                    unitsToReserve.Add(("image", UsageReservationKeys.Image(requestKey, i), options.ImageUnits));
            foreach (var (operation, key, units) in unitsToReserve)
                db.UsageEvents.Add(new UsageEvent { TeacherId = teacherId, Operation = operation, RequestKey = key,
                    ParentRequestKey = requestKey, Units = units, IsRateLimitedRequest = true,
                    IsReservation = true, Succeeded = false, CreatedAtUtc = now,
                    LeaseExpiresAtUtc = now.AddMinutes(Math.Max(1, options.ReservationLeaseMinutes)) });
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    public async Task CompleteReservationAsync(int teacherId, string operation, string requestKey, bool succeeded, int units = 1, int promptTokens = 0, int completionTokens = 0, CancellationToken ct = default)
    {
        var reservation = await db.UsageEvents.SingleOrDefaultAsync(x => x.TeacherId == teacherId && x.Operation == operation && x.RequestKey == requestKey, ct);
        if (reservation is null) return;
        if (!reservation.IsReservation) return;
        if (!succeeded) { db.UsageEvents.Remove(reservation); await db.SaveChangesAsync(ct); return; }
        reservation.IsReservation = false; reservation.Succeeded = true; reservation.Units = units; reservation.CreatedAtUtc = DateTime.UtcNow;
        reservation.PromptTokens = promptTokens; reservation.CompletionTokens = completionTokens;
        reservation.CostStatus = "Unknown";
        reservation.OutcomeDetail = "provider-cost-unreported";
        await db.SaveChangesAsync(ct);
    }

    public async Task ReleaseReservationsAsync(string requestKey, CancellationToken ct = default)
    {
        var rows = await db.UsageEvents.Where(x => (x.ParentRequestKey == requestKey || x.RequestKey == requestKey) && x.IsReservation).ToListAsync(ct);
        db.UsageEvents.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
    }

    public async Task ReconcileOrphanedReservationsAsync(IReadOnlySet<string> activeRequestKeys, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expired = await db.UsageEvents.Where(x => x.IsReservation && x.LeaseExpiresAtUtc != null && x.LeaseExpiresAtUtc <= now).ToListAsync(ct);
        foreach (var row in expired)
        {
            if (row.ParentRequestKey is not null && activeRequestKeys.Contains(row.ParentRequestKey))
            {
                row.LeaseExpiresAtUtc = now.AddMinutes(Math.Max(1, options.ReservationLeaseMinutes));
                continue;
            }
            db.UsageEvents.Remove(row);
        }
        var cutoff = now.AddMinutes(-Math.Max(1, options.ReservationLeaseMinutes));
        var abandonedEvents = await db.UsageEvents.Where(x => x.ParentRequestKey != null && x.Operation != "request"
            && x.CreatedAtUtc <= cutoff && x.IsRateLimitedRequest && !x.IsReservation && !x.Succeeded).ToListAsync(ct);
        foreach (var row in abandonedEvents)
            if (row.ParentRequestKey is null || !activeRequestKeys.Contains(row.ParentRequestKey)) db.UsageEvents.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    public async Task PruneOldRequestMarkersAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-Math.Max(1, options.ShortWindowMinutes));
        await db.UsageEvents.Where(x => x.IsRateLimitedRequest && x.CreatedAtUtc < cutoff
            && !db.UsageEvents.Any(r => r.ParentRequestKey == x.ParentRequestKey && r.IsReservation)).ExecuteDeleteAsync(ct);
    }

    public async Task<bool> CanReserveAsync(int teacherId, IReadOnlyCollection<string> operations, CancellationToken ct = default, int imageReservations = 1)
    {
        if (!options.Enabled) return true;
        var usage = await GetAsync(teacherId, ct);
        var reset = ResetStart(DateTime.UtcNow);
        var pending = await db.UsageEvents.AsNoTracking().Where(x => x.TeacherId == teacherId && x.IsReservation && x.CreatedAtUtc >= reset).ToListAsync(ct);
        var textNeeded = operations.Where(x => x != "image").Sum(OperationUnits) + pending.Where(x => x.Operation != "image").Sum(x => x.Units);
        var requestedImageCount = operations.Count(x => x == "image");
        if (operations.Contains("image-prompt") && requestedImageCount > 0) requestedImageCount = Math.Clamp(imageReservations, 1, 20);
        var imageNeeded = requestedImageCount * options.ImageUnits + pending.Where(x => x.Operation == "image").Sum(x => x.Units);
        return usage.TextUsed + textNeeded <= options.DailyTextUnits && usage.ImageUsed + imageNeeded <= options.DailyImageUnits;
    }

    private int OperationUnits(string operation) => operation == "regenerate" ? options.RegenerateUnits : options.TextSectionUnits;


    public async Task RecordAsync(int teacherId, string operation, string requestKey, bool succeeded,
        int units = 1, int promptTokens = 0, int completionTokens = 0, int imageCount = 0, CancellationToken ct = default)
    {
        if (await db.UsageEvents.AnyAsync(x => x.RequestKey == requestKey, ct)) return;
        db.UsageEvents.Add(new UsageEvent { TeacherId = teacherId, Operation = operation, RequestKey = requestKey,
            Units = succeeded ? units : 0, PromptTokens = promptTokens, CompletionTokens = completionTokens,
            ImageCount = imageCount, Succeeded = succeeded, CreatedAtUtc = DateTime.UtcNow });
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { db.ChangeTracker.Clear(); }
    }

    public async Task<bool> WithinShortWindowAsync(int teacherId, CancellationToken ct = default)
    {
        if (!options.Enabled) return true;
        var since = DateTime.UtcNow.AddMinutes(-Math.Max(1, options.ShortWindowMinutes));
        return await db.UsageEvents.CountAsync(x => x.TeacherId == teacherId && x.IsRateLimitedRequest && x.CreatedAtUtc >= since, ct) < options.ShortWindowRequests;
    }

    public async Task<bool> IsEnabledAsync(int teacherId, string operation, string requestKey, CancellationToken ct = default) =>
        !options.Enabled || await db.UsageEvents.AnyAsync(x => x.TeacherId == teacherId && x.Operation == operation
            && x.RequestKey == requestKey && x.IsReservation, ct);

    public Task<bool> HasReservationAsync(int teacherId, string operation, string requestKey, CancellationToken ct = default) =>
        IsEnabledAsync(teacherId, operation, requestKey, ct);


    private async Task<T> InImmediateTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(ct);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
                await using var efTransaction = await db.Database.UseTransactionAsync(transaction, ct);
                try
                {
                    var result = await action();
                    await efTransaction!.CommitAsync(ct);
                    return result;
                }
                catch
                {
                    try { await efTransaction!.RollbackAsync(CancellationToken.None); } catch { }
                    db.ChangeTracker.Clear();
                    throw;
                }
            }
            catch (SqliteException ex) when ((ex.SqliteErrorCode == 5 || ex.SqliteErrorCode == 6) && attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(40 * (attempt + 1)), ct);
            }
        }
    }

    private DateTime ResetStart(DateTime utc)
    {
        var today = utc.Date.AddHours(Math.Clamp(options.ResetUtcHour, 0, 23));
        return utc < today ? today.AddDays(-1) : today;
    }
}

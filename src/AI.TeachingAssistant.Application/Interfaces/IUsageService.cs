namespace AI.TeachingAssistant.Application.Interfaces;

public interface IUsageService
{
    Task<bool> CanReserveAsync(int teacherId, IReadOnlyCollection<string> operations, CancellationToken ct = default, int imageReservations = 1);
    Task RecordAsync(int teacherId, string operation, string requestKey, bool succeeded, int units = 1,
        int promptTokens = 0, int completionTokens = 0, int imageCount = 0, CancellationToken ct = default);
    Task<bool> WithinShortWindowAsync(int teacherId, CancellationToken ct = default);
    Task<bool> ReserveAsync(int teacherId, IReadOnlyCollection<string> operations, string requestKey, CancellationToken ct = default);
    Task<bool> AdmitAsync(int teacherId, IReadOnlyCollection<string> operations, string requestKey, CancellationToken ct = default, int imageReservations = 1);
    Task ReleaseReservationsAsync(string requestKey, CancellationToken ct = default);
    Task CompleteReservationAsync(int teacherId, string operation, string requestKey, bool succeeded, int units = 1, int promptTokens = 0, int completionTokens = 0, CancellationToken ct = default);
    Task<UsageSnapshot> GetAsync(int teacherId, CancellationToken ct = default);
    Task ReconcileOrphanedReservationsAsync(IReadOnlySet<string> activeRequestKeys, CancellationToken ct = default);
    Task<bool> HasReservationAsync(int teacherId, string operation, string requestKey, CancellationToken ct = default);
    Task<bool> IsEnabledAsync(int teacherId, string operation, string requestKey, CancellationToken ct = default);
    Task PruneOldRequestMarkersAsync(CancellationToken ct = default);
}

public sealed record UsageSnapshot(int TextLimit, int TextUsed, int ImageLimit, int ImageUsed,
    DateTime ResetsAtUtc, int WarningPercent, IReadOnlyDictionary<string, int> Operations);

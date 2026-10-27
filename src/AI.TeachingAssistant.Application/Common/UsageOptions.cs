namespace AI.TeachingAssistant.Application.Common;

public sealed class UsageOptions
{
    public const string SectionName = "Usage";
    public bool Enabled { get; set; } = true;
    public int DailyTextUnits { get; set; } = 120;
    public int DailyImageUnits { get; set; } = 24;
    public int TextSectionUnits { get; set; } = 1;
    public int RegenerateUnits { get; set; } = 1;
    public int ImageUnits { get; set; } = 1;
    public int WarningPercent { get; set; } = 80;
    public int ShortWindowRequests { get; set; } = 8;
    public int ShortWindowMinutes { get; set; } = 1;
    public int ResetUtcHour { get; set; }
    public int ReservationLeaseMinutes { get; set; } = 120;
    public int ReconciliationIntervalMinutes { get; set; } = 5;
    public int LeaseHeartbeatSeconds { get; set; } = 30;
    public string WorkerId { get; set; } = string.Empty;
}

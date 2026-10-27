namespace AI.TeachingAssistant.Domain.Entities;

/// <summary>One completed or failed metered operation. Units are configurable policy units, not currency.</summary>
public sealed class UsageEvent
{
    public long Id { get; set; }
    public int TeacherId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string RequestKey { get; set; } = string.Empty;
    public int Units { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int ImageCount { get; set; }
    public bool Succeeded { get; set; }
    public bool IsReservation { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public string? ParentRequestKey { get; set; }
    public bool IsRateLimitedRequest { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public int? ProviderCostMicros { get; set; }
    public string CostStatus { get; set; } = "Unknown";
    public string? OutcomeDetail { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

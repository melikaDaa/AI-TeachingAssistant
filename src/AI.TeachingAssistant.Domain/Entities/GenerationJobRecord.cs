namespace AI.TeachingAssistant.Domain.Entities;

public enum GenerationJobStatus { Pending = 0, Processing = 1, Succeeded = 2, Failed = 3 }

/// <summary>Durable queue envelope and current fenced lease.</summary>
public sealed class GenerationJobRecord
{
    public long Id { get; set; }
    public string RequestKey { get; set; } = string.Empty;
    public int LessonId { get; set; }
    public int TeacherId { get; set; }
    public int SectionType { get; set; }
    public GenerationJobStatus Status { get; set; }
    public string? OwnerId { get; set; }
    public long FencingToken { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public string? FailureCode { get; set; }
}

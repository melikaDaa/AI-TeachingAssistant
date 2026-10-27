namespace AI.TeachingAssistant.Domain.Entities;

public sealed class PhoneVerificationChallenge : BaseEntity
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public int FailedAttempts { get; set; }
    public string RequestIpHash { get; set; } = string.Empty;

    public bool IsRegistration { get; set; }
}

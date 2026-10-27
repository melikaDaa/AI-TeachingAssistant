namespace AI.TeachingAssistant.Infrastructure.Ai;

public sealed class KavenegarOptions
{
    public const string SectionName = "Kavenegar";
    public string BaseUrl { get; set; } = "https://api.kavenegar.com/v1/";
    public string ApiKey { get; set; } = string.Empty;
    /// <summary>Exact approved verification template name configured in Kavenegar console.</summary>
    public string OtpTemplate { get; set; } = string.Empty;
}

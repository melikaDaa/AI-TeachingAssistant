namespace AI.TeachingAssistant.Application.Common;

public interface IPhoneOtpSender
{
    Task SendAsync(string phoneNumber, string code, CancellationToken cancellationToken = default);
}

public interface IDevelopmentOtpCodeReader
{
    bool TryGetCode(string phoneNumber, out string code);
}

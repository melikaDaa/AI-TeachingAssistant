using System.Collections.Concurrent;
using AI.TeachingAssistant.Application.Common;
using Microsoft.Extensions.Hosting;

namespace AI.TeachingAssistant.Infrastructure.Security;

/// <summary>Development-only OTP transport. The generated code is available only through the local dev UI endpoint.</summary>
public sealed class DevelopmentFakeOtpSender : IPhoneOtpSender, IDevelopmentOtpCodeReader
{
    private readonly ConcurrentDictionary<string, (string Code, DateTime ExpiresAt)> _codes = new(StringComparer.Ordinal);
    private readonly IHostEnvironment _environment;

    public DevelopmentFakeOtpSender(IHostEnvironment environment) => _environment = environment;

    public Task SendAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
            throw new InvalidOperationException("Development OTP transport is not available outside Development.");
        _codes[phoneNumber] = (code, DateTime.UtcNow.AddMinutes(5));
        return Task.CompletedTask;
    }

    public bool TryGetCode(string phoneNumber, out string code)
    {
        code = string.Empty;
        if (!_environment.IsDevelopment() || !_codes.TryGetValue(phoneNumber, out var entry)) return false;
        if (entry.ExpiresAt <= DateTime.UtcNow)
        {
            _codes.TryRemove(phoneNumber, out _);
            return false;
        }
        code = entry.Code;
        return true;
    }
}

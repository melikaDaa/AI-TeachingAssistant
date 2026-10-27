using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace AI.TeachingAssistant.Infrastructure.Security;

public interface IDevelopmentOtpAvailability
{
    bool IsAvailable { get; }
}

public sealed class DevelopmentOtpAvailability(IHostEnvironment environment, IConfiguration configuration) : IDevelopmentOtpAvailability
{
    public bool IsAvailable => environment.IsDevelopment() && configuration.GetValue<bool>("DevelopmentAuth:UseFakeOtpSender");
}

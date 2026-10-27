using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Infrastructure.Ai;
using AI.TeachingAssistant.Infrastructure.Persistence;
using AI.TeachingAssistant.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace AI.TeachingAssistant.Tests;

public sealed class PhoneOtpServiceTests
{
    [Fact]
    public async Task Wrong_codes_lock_after_five_attempts_and_correct_code_cannot_be_reused()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:OtpHashSecret"] = "long-test-secret",
            ["Auth:RateLimitPepper"] = "rate-test-secret",
        }).Build();
        var sms = new FakeOtpSender();
        var service = new PhoneOtpService(db, sms, config, Options.Create(new KavenegarOptions
        {
            ApiKey = "fake", OtpTemplate = "approved-test-template"
        }));

        await service.RequestAsync("09123456789", "127.0.0.1", default);
        var hash = db.PhoneVerificationChallenges.Single().CodeHash;
        Assert.DoesNotContain(sms.Code, hash, StringComparison.Ordinal);
        for (var i = 0; i < 5; i++)
            await Assert.ThrowsAsync<OtpRequestException>(() => service.VerifyAsync("09123456789", "000000", "127.0.0.1", default));
        await Assert.ThrowsAsync<OtpRequestException>(() => service.VerifyAsync("09123456789", sms.Code, "127.0.0.1", default));
        Assert.Empty(db.Teachers);
    }

    [Fact]
    public async Task Correct_code_creates_account_once_then_is_consumed()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:OtpHashSecret"] = "long-test-secret",
            ["Auth:RateLimitPepper"] = "rate-test-secret",
        }).Build();
        var sms = new FakeOtpSender();
        var service = new PhoneOtpService(db, sms, config, Options.Create(new KavenegarOptions
        {
            ApiKey = "fake", OtpTemplate = "approved-test-template"
        }));
        await service.RequestAsync("09123456789", "127.0.0.1", default);
        var teacher = await service.VerifyAsync("09123456789", sms.Code, "127.0.0.1", default);
        Assert.Equal("+989123456789", teacher.PhoneNumber);
        await Assert.ThrowsAsync<OtpRequestException>(() => service.VerifyAsync("09123456789", sms.Code, "127.0.0.1", default));
        Assert.Single(db.Teachers);
    }

    [Fact]
    public async Task Failed_sms_delivery_does_not_allow_verification_or_account_creation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:OtpHashSecret"] = "long-test-secret",
            ["Auth:RateLimitPepper"] = "rate-test-secret",
        }).Build();
        var sender = new FailingOtpSender();
        var service = new PhoneOtpService(db, sender, config, Options.Create(new KavenegarOptions
        {
            ApiKey = "fake", OtpTemplate = "approved-test-template"
        }));
        await Assert.ThrowsAsync<OtpRequestException>(() => service.RequestAsync("09123456789", "127.0.0.1", default));
        Assert.Empty(db.Teachers);
        Assert.NotNull(db.PhoneVerificationChallenges.Single().ConsumedAt);
    }

    private sealed class FakeOtpSender : IPhoneOtpSender
    {
        public string Code { get; private set; } = "";
        public Task SendAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
        {
            Code = code;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingOtpSender : IPhoneOtpSender
    {
        public Task SendAsync(string phoneNumber, string code, CancellationToken cancellationToken = default) =>
            throw new KavenegarDeliveryException("fake failure", false);
    }
}

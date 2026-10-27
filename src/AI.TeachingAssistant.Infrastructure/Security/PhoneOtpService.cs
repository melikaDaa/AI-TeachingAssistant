using System.Security.Cryptography;
using System.Text;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Infrastructure.Persistence;
using AI.TeachingAssistant.Infrastructure.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AI.TeachingAssistant.Infrastructure.Security;

public sealed class PhoneOtpService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    private const int MaxFailedAttempts = 5;
    private const int MaxPhoneRequestsPerHour = 5;
    private const int MaxIpRequestsPerHour = 20;

    private readonly AppDbContext _db;
    private readonly IPhoneOtpSender _sender;
    private readonly IConfiguration _configuration;
    private readonly KavenegarOptions _kavenegar;

    public PhoneOtpService(AppDbContext db, IPhoneOtpSender sender, IConfiguration configuration,
        IOptions<KavenegarOptions> kavenegar)
    {
        _db = db;
        _sender = sender;
        _configuration = configuration;
        _kavenegar = kavenegar.Value;
    }

    public async Task RequestAsync(string? rawPhone, string? ip, CancellationToken ct)
    {
        var phone = PhoneNumberNormalizer.NormalizeIranianMobile(rawPhone);
        if (phone is null) throw new OtpRequestException("شماره تلفن همراه معتبر وارد کنید.", 400);
        if (_sender is KavenegarOtpSender && (string.IsNullOrWhiteSpace(_kavenegar.ApiKey) || string.IsNullOrWhiteSpace(_kavenegar.OtpTemplate)))
            throw new OtpRequestException("تنظیمات Kavenegar کامل نیست. کلید API و نام الگوی تأیید را در تنظیمات امن وارد کنید.", 503);

        var now = DateTime.UtcNow;
        var ipHash = Hash(_configuration["Auth:RateLimitPepper"] ?? _configuration["InternalAssertion:Secret"] ?? string.Empty, ip ?? "unknown");
        var phoneHistory = await _db.PhoneVerificationChallenges.Where(x => x.PhoneNumber == phone && x.CreatedAt > now.AddHours(-1)).ToListAsync(ct);
        var ipHistory = await _db.PhoneVerificationChallenges.Where(x => x.RequestIpHash == ipHash && x.CreatedAt > now.AddHours(-1)).CountAsync(ct);
        if (phoneHistory.Count >= MaxPhoneRequestsPerHour || ipHistory >= MaxIpRequestsPerHour ||
            phoneHistory.Any(x => x.CreatedAt > now.Subtract(ResendCooldown)))
            throw new OtpRequestException("برای درخواست کد کمی صبر کنید و دوباره تلاش کنید.", 429);

        var teacherExists = await _db.Teachers.AnyAsync(x => x.PhoneNumber == phone && x.IsActive, ct);
        var isRegistration = !teacherExists;
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var secret = _configuration["Auth:OtpHashSecret"];
        if (string.IsNullOrWhiteSpace(secret)) throw new OtpRequestException("Authentication is not configured.", 503);

        var challenge = new PhoneVerificationChallenge
        {
            PhoneNumber = phone,
            CodeHash = Hash(secret, $"{phone}:{code}"),
            ExpiresAt = now.Add(CodeLifetime),
            FailedAttempts = 0,
            RequestIpHash = ipHash,
            IsRegistration = isRegistration,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.PhoneVerificationChallenges.Add(challenge);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _sender.SendAsync(phone, code, ct);
        }
        catch (KavenegarDeliveryException ex)
        {
            challenge.ConsumedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw new OtpRequestException(ex.IsTransient
                ? "SMS delivery is temporarily unavailable. Please request a new code later."
                : "ارسال پیامک انجام نشد. کلید API و نام الگوی تأییدشده Kavenegar را بررسی کنید.", ex.IsTransient ? 503 : 502);
        }
        catch (Exception exception) when (exception is not OtpRequestException && !ct.IsCancellationRequested)
        {
            challenge.ConsumedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw new OtpRequestException("SMS delivery could not be completed. Please try again later.", 503);
        }
    }

    public async Task<Teacher> VerifyAsync(string? rawPhone, string? rawCode, string? ip, CancellationToken ct)
    {
        var phone = PhoneNumberNormalizer.NormalizeIranianMobile(rawPhone);
        if (phone is null || string.IsNullOrWhiteSpace(rawCode) || rawCode.Length != 6 || rawCode.Any(c => c is < '0' or > '9'))
            throw new OtpRequestException("شماره یا کد تأیید معتبر نیست.", 400);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var challenge = await _db.PhoneVerificationChallenges
            .Where(x => x.PhoneNumber == phone && x.ConsumedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (challenge is null || challenge.ExpiresAt <= now || challenge.FailedAttempts >= MaxFailedAttempts)
            throw new OtpRequestException("کد تأیید منقضی یا نامعتبر است. درخواست کد تازه کنید.", 400);

        var secret = _configuration["Auth:OtpHashSecret"];
        if (string.IsNullOrWhiteSpace(secret)) throw new OtpRequestException("Authentication is not configured.", 503);
        var expected = Convert.FromHexString(challenge.CodeHash);
        var actual = Convert.FromHexString(Hash(secret, $"{phone}:{rawCode}"));
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            challenge.FailedAttempts++;
            challenge.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            throw new OtpRequestException("کد تأیید معتبر نیست.", 400);
        }

        challenge.ConsumedAt = now;
        challenge.UpdatedAt = now;
        Teacher teacher;
        if (challenge.IsRegistration)
        {
            if (await _db.Teachers.AnyAsync(x => x.PhoneNumber == phone, ct))
                throw new OtpRequestException("امکان ثبت‌نام با این شماره وجود ندارد.", 409);
            teacher = new Teacher
            {
                PhoneNumber = phone,
                FullName = "معلم عزیز",
                Level = "متوسطه اول",
                Grade = "هفتم",
                Subjects = "",
                TeachingStyle = "تعاملی",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _db.Teachers.Add(teacher);
        }
        else
        {
            teacher = await _db.Teachers.FirstOrDefaultAsync(x => x.PhoneNumber == phone && x.IsActive, ct)
                ?? throw new OtpRequestException("حساب فعال یافت نشد.", 401);
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return teacher;
    }

    private static string Hash(string key, string value) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(value)));

}

public sealed class OtpRequestException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

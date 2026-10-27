using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AI.TeachingAssistant.Infrastructure.Security;

public sealed class PasswordAuthenticationService(AppDbContext db, IPasswordHasher<Teacher> hasher)
{
    public async Task<Teacher> RegisterAsync(RegisterAccountRequest request, CancellationToken ct)
    {
        var name = request.FullName?.Trim();
        var identifier = NormalizeIdentifier(request.Identifier);
        ValidateName(name);
        ValidateEmail(identifier);
        ValidatePassword(request.Password);
        if (request.ConfirmPassword is null || !string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            throw new AccountException("تکرار گذرواژه با گذرواژه یکسان نیست.", 400);

        var email = identifier;
        if (await db.Teachers.AnyAsync(t => t.Email != null && EF.Functions.Collate(t.Email, "NOCASE") == email, ct))
            throw new AccountException("با این ایمیل قبلاً حساب ساخته شده است.", 409);

        var teacher = new Teacher
        {
            FullName = name!,
            Email = email,
            PhoneNumber = null,
            PasswordHash = "pending",
            Level = "متوسطه اول",
            Grade = "هفتم",
            Subjects = "",
            TeachingStyle = "تعاملی",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        teacher.PasswordHash = hasher.HashPassword(teacher, request.Password!);
        db.Teachers.Add(teacher);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new AccountException("با این ایمیل قبلاً حساب ساخته شده است.", 409); }
        return teacher;
    }

    public async Task<Teacher> LoginAsync(LoginAccountRequest request, CancellationToken ct)
    {
        var identifier = NormalizeIdentifier(request.Identifier);
        if (!System.Net.Mail.MailAddress.TryCreate(identifier, out var address) || address.Address != identifier)
            throw new AccountException("ایمیل یا گذرواژه درست نیست.", 401);
        var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.IsActive && t.Email != null && EF.Functions.Collate(t.Email, "NOCASE") == identifier, ct);
        var password = request.Password;
        if (teacher?.PasswordHash is null || string.IsNullOrWhiteSpace(password) ||
            hasher.VerifyHashedPassword(teacher, teacher.PasswordHash, password) == PasswordVerificationResult.Failed)
            throw new AccountException("ایمیل یا گذرواژه درست نیست.", 401);

        if (hasher.VerifyHashedPassword(teacher, teacher.PasswordHash, password) == PasswordVerificationResult.SuccessRehashNeeded)
        {
            teacher.PasswordHash = hasher.HashPassword(teacher, password);
            await db.SaveChangesAsync(ct);
        }
        return teacher;
    }

    private static string NormalizeIdentifier(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
    private static void ValidateName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 150)
            throw new AccountException("نام و نام خانوادگی را وارد کنید (حداکثر ۱۵۰ نویسه).", 400);
    }
    private static void ValidateEmail(string value)
    {
        if (value.Length is < 5 or > 254) throw new AccountException("نشانی ایمیل معتبر نیست.", 400);
        if (!System.Net.Mail.MailAddress.TryCreate(value, out var address) || address.Address != value)
            throw new AccountException("نشانی ایمیل معتبر نیست.", 400);
    }
    private static void ValidatePassword(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 12 || value.Length > 128)
            throw new AccountException("گذرواژه باید بین ۱۲ تا ۱۲۸ نویسه باشد.", 400);
        if (!value.Any(char.IsLetter) || !value.Any(char.IsDigit))
            throw new AccountException("گذرواژه باید دست‌کم یک حرف و یک عدد داشته باشد.", 400);
    }
}

public sealed record RegisterAccountRequest(string? FullName, string? Identifier, string? Password, string? ConfirmPassword);
public sealed record LoginAccountRequest(string? Identifier, string? Password, bool RememberMe = true);
public sealed class AccountException(string message, int statusCode) : Exception(message) { public int StatusCode { get; } = statusCode; }

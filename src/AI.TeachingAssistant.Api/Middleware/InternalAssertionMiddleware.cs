using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Infrastructure.Security;

namespace AI.TeachingAssistant.Api.Middleware;

public sealed class InternalAssertionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly InternalAssertionOptions _options;

    public InternalAssertionMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _options = configuration.GetSection("InternalAssertion").Get<InternalAssertionOptions>() ?? new();
    }

    public async Task InvokeAsync(HttpContext context, ICurrentTeacherContext currentTeacher)
    {
        // This middleware bridges authenticated server-side Web HttpClient calls to API
        // controllers. Public Blazor pages and static assets must reach their own routing.
        if (!context.Request.Path.StartsWithSegments("/api") ||
            context.Request.Path.StartsWithSegments("/api/account") ||
            context.Request.Path.StartsWithSegments("/api/images") ||
            context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }
        if (string.IsNullOrWhiteSpace(_options.Secret) ||
            !int.TryParse(context.Request.Headers["X-MadreseAI-Teacher"], out var teacherId) || teacherId <= 0 ||
            context.Request.Headers["X-MadreseAI-Issuer"] != "madreseai-web" ||
            context.Request.Headers["X-MadreseAI-Audience"] != "madreseai-api" ||
            !Guid.TryParseExact(context.Request.Headers["X-MadreseAI-Nonce"], "N", out _) ||
            !long.TryParse(context.Request.Headers["X-MadreseAI-Expiry"], out var expiry) ||
            expiry <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() ||
            expiry > DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds())
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var signature = context.Request.Headers["X-MadreseAI-Signature"].ToString();
        byte[] supplied;
        try { supplied = Convert.FromHexString(signature); }
        catch (FormatException)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var method = context.Request.Method.ToUpperInvariant();
        var path = context.Request.Path + context.Request.QueryString;
        var issuer = context.Request.Headers["X-MadreseAI-Issuer"].ToString();
        var audience = context.Request.Headers["X-MadreseAI-Audience"].ToString();
        var nonce = context.Request.Headers["X-MadreseAI-Nonce"].ToString();
        var payload = $"{teacherId}\n{issuer}\n{audience}\n{expiry}\n{nonce}\n{method}\n{path}";
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_options.Secret), Encoding.UTF8.GetBytes(payload));
        if (supplied.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(supplied, expected))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        currentTeacher.Set(teacherId);
        context.Items["InternalAssertionValidated"] = true;
        // The in-process Web→API bridge request carries no auth cookie: the validated HMAC
        // assertion is its credential. Mark the request authenticated (same claim shape as the
        // cookie principal) so endpoint authorization — including the authenticated-user
        // fallback policy — accepts it; without this, UseAuthorization challenges with 401
        // before any controller runs.
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, teacherId.ToString()),
            new Claim("teacher_id", teacherId.ToString()),
            new Claim("iss", issuer),
            new Claim("aud", audience),
        ], "InternalAssertion"));
        await _next(context);
    }
}

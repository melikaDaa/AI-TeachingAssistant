using System.Security.Claims;
using AI.TeachingAssistant.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace AI.TeachingAssistant.Api.Controllers;

[ApiController]
[Route("api/account")]
public sealed class AccountController(PasswordAuthenticationService passwords) : ControllerBase
{
    [HttpPost("register")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Register([FromBody] RegisterAccountRequest request, CancellationToken ct)
    {
        try
        {
            if (!await ValidateRequestTokenAsync()) return BadRequest(new { error = "درخواست معتبر نیست؛ صفحه را تازه‌سازی کنید." });
            var teacher = await passwords.RegisterAsync(request, ct);
            try { await SignInAsync(teacher, true); }
            catch (AccountException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
            return Ok(new { teacher.Id, teacher.FullName });
        }
        catch (AccountException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpPost("login")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Login([FromBody] LoginAccountRequest request, CancellationToken ct)
    {
        try
        {
            if (!await ValidateRequestTokenAsync()) return BadRequest(new { error = "درخواست معتبر نیست؛ صفحه را تازه‌سازی کنید." });
            var teacher = await passwords.LoginAsync(request, ct);
            try { await SignInAsync(teacher, request.RememberMe); }
            catch (AccountException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
            return Ok(new { teacher.Id, teacher.FullName });
        }
        catch (AccountException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    private async Task SignInAsync(AI.TeachingAssistant.Domain.Entities.Teacher teacher, bool rememberMe)
    {
        var isLocalDevelopment = HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true &&
            (Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || Request.Host.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase));
        if (!Request.IsHttps && !isLocalDevelopment)
            throw new AccountException("ورود امن فقط از طریق HTTPS امکان‌پذیر است.", 400);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, teacher.Id.ToString()),
            new Claim("teacher_id", teacher.Id.ToString()),
            new Claim(ClaimTypes.Name, teacher.FullName),
            new Claim("iss", "madreseai-web"), new Claim("aud", "madreseai-api"),
        };
        if (teacher.PhoneNumber is not null) claims = [.. claims, new Claim(ClaimTypes.MobilePhone, teacher.PhoneNumber)];
        if (teacher.Email is not null) claims = [.. claims, new Claim(ClaimTypes.Email, teacher.Email)];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = rememberMe, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14) });
    }

    private async Task<bool> ValidateRequestTokenAsync()
    {
        try
        {
            var antiforgery = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
            var tokens = antiforgery.GetAndStoreTokens(HttpContext);
            await antiforgery.ValidateRequestAsync(HttpContext);
            return true;
        }
        catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException) { return false; }
    }

    [HttpGet("csrf")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public IActionResult Csrf(Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Headers.CacheControl = "no-store";
        return Ok(new { token = tokens.RequestToken });
    }

    [HttpPost("logout")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout()
    {
        var isLocalDevelopment = HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true &&
            (Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || Request.Host.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase));
        if (!Request.IsHttps && !isLocalDevelopment)
            return BadRequest(new { error = "ورود امن فقط از طریق HTTPS امکان‌پذیر است." });
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

}

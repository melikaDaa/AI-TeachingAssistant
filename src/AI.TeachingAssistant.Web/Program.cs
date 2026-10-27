using AI.TeachingAssistant.Infrastructure;
using AI.TeachingAssistant.Infrastructure.Persistence;
using AI.TeachingAssistant.Web.Components;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using AI.TeachingAssistant.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "MadreseAI.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
});
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, CookieAuthenticationStateProvider>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = IsLocalHttpDevelopment(builder.Environment)
        ? "MadreseAI.Auth"
        : "__Host-MadreseAI.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = IsLocalHttpDevelopment(builder.Environment)
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
    options.Cookie.IsEssential = true;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.LoginPath = "/account/login";
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        context.Response.Redirect(options.LoginPath + "?returnUrl=" + Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthenticationStateProvider, CookieAuthenticationStateProvider>();
builder.Services.AddScoped<InternalAssertionHandler>();

// Serve the REST API from the same host so one `dotnet run` runs the whole MVP.
// Controllers live in the Api project; register that assembly for controller discovery.
builder.Services.AddControllers()
    .AddApplicationPart(typeof(AI.TeachingAssistant.Api.Controllers.LessonsController).Assembly);
// Defense in depth: every /api controller except the account/images paths must show the
// middleware-validated assertion marker, even if middleware ordering is changed later.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
    options.Filters.Add(new AI.TeachingAssistant.Api.RequireInternalAssertionFilter()));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("ai", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("general", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 200, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// Friendly Persian validation errors instead of the default English ProblemDetails.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();
        var message = errors.Count > 0 && errors.All(e => IsPersian(e))
            ? string.Join(" ", errors)
            : "لطفاً اطلاعات وارد شده را بررسی و دوباره تلاش کنید.";
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new { error = message });
    };
});

static bool IsPersian(string text) => text.Any(c => c > 0x0600 && c < 0x06FF);
static bool IsLocalHttpDevelopment(IWebHostEnvironment environment) =>
    environment.IsDevelopment() || environment.IsEnvironment("LocalHttpDevelopment");

// The UI calls this same host's REST API in-process through MadreseAIApiClient.
// When `InternalApi:BaseUrl` is not configured, derive the address from what this
// host actually listens on (launchSettings port locally, ASPNETCORE_URLS in Docker)
// instead of assuming port 80, which only exists inside the container.
static string ResolveInternalApiBaseUrl(IConfiguration configuration)
{
    var configured = configuration["InternalApi:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(configured))
        return configured;

    foreach (var entry in (configuration["urls"] ?? "")
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var scheme = "http";
        var rest = entry;
        var schemeEnd = entry.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd > 0)
        {
            scheme = entry[..schemeEnd];
            rest = entry[(schemeEnd + 3)..];
        }

        string host;
        string portText;
        if (rest.StartsWith('[')) // IPv6 literal, e.g. [::]:5000
        {
            var close = rest.IndexOf(']');
            if (close <= 0) continue;
            host = rest[..(close + 1)];
            var after = rest[(close + 1)..];
            portText = after.StartsWith(':') ? after[1..] : "";
        }
        else
        {
            var portStart = rest.LastIndexOf(':');
            host = portStart > 0 ? rest[..portStart] : rest;
            portText = portStart > 0 ? rest[(portStart + 1)..] : "";
        }

        // Kestrel wildcard binds aren't client addresses; this call stays in-process.
        if (host is "+" or "*" or "0.0.0.0" or "[::]" or "::")
            host = "127.0.0.1";
        if (!int.TryParse(portText, out var port) || port <= 0)
            port = Uri.UriSchemeHttps.Equals(scheme, StringComparison.OrdinalIgnoreCase) ? 443 : 80;

        var candidate = $"{scheme}://{host}:{port}/";
        if (Uri.IsWellFormedUriString(candidate, UriKind.Absolute))
            return candidate;
    }

    return "http://127.0.0.1:5000/";
}

builder.Services.AddTeachingAssistantInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());

builder.Services.AddHttpClient<AI.TeachingAssistant.Web.Services.MadreseAIApiClient>(http =>
{
    http.BaseAddress = new Uri(ResolveInternalApiBaseUrl(builder.Configuration));
}).AddHttpMessageHandler<AI.TeachingAssistant.Web.Services.InternalAssertionHandler>();

var app = builder.Build();

// The in-process Web→API bridge (InternalAssertionHandler/Middleware) needs a shared
// HMAC secret. Without it every UI data call (profile, lessons, generation) fails with
// 401/500 while login still works — surface the misconfiguration immediately.
if (string.IsNullOrWhiteSpace(builder.Configuration["InternalAssertion:Secret"]))
{
    app.Logger.LogError(
        "InternalAssertion:Secret is not configured, so every UI data call (profile, lessons, generation) will fail. " +
        "Fix locally with: dotnet user-secrets set \"InternalAssertion:Secret\" \"<long-random-value>\" " +
        "--project src/AI.TeachingAssistant.Web/AI.TeachingAssistant.Web.csproj " +
        "(production: set the InternalAssertion__Secret environment variable).");
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DatabaseInitializer.InitializeAsync(db);
}

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // The UI ships a single mutable app.css (and other non-fingerprinted assets).
        // Without explicit cache headers browsers apply heuristic freshness and can keep
        // a stale stylesheet across UI updates — which silently breaks responsive nav.
        // Force revalidation (cheap 304s via ETag) so updated CSS always wins.
        var name = ctx.File.Name;
        if (name.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache, must-revalidate";
        }
    }
});
app.UseRouting();
// In production, reject plain-HTTP account requests before antiforgery attempts to
// issue a Secure cookie (which otherwise fails inside GetAndStoreTokens).
app.Use(async (context, next) =>
{
    if (!IsLocalHttpDevelopment(builder.Environment) &&
        context.Request.Path.StartsWithSegments("/api/account") &&
        !context.Request.IsHttps)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = "برای ورود و ثبت‌نام از اتصال HTTPS استفاده کنید." });
        return;
    }

    await next();
});
app.UseAuthentication();
// The assertion middleware must run BEFORE UseAuthorization: it validates the bridge's HMAC
// and marks the request authenticated, otherwise the authenticated-user fallback policy
// challenges cookie-less in-process API calls with 401 before controllers ever run.
app.UseMiddleware<AI.TeachingAssistant.Api.Middleware.InternalAssertionMiddleware>();
app.UseAuthorization();
app.UseAntiforgery();
app.UseRateLimiter();

// Same Persian error envelope as the standalone Api host (spec §18).
app.UseMiddleware<AI.TeachingAssistant.Api.Middleware.ErrorHandlingMiddleware>();

app.MapControllers().RequireRateLimiting("general");
app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

using System.Threading.RateLimiting;
using AI.TeachingAssistant.Api.Middleware;
using AI.TeachingAssistant.Infrastructure;
using AI.TeachingAssistant.Infrastructure.Persistence;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using AI.TeachingAssistant.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddControllers();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "__Host-MadreseAI.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
    options.Cookie.IsEssential = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});
builder.Services.AddAuthorization(options => options.FallbackPolicy =
    new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.Configure<MvcOptions>(options => options.Filters.Add(new RequireInternalAssertionFilter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Basic request limit for AI usage (spec §19/§20).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("ai", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    options.AddPolicy("general", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 200,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddTeachingAssistantInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());

var app = builder.Build();

// Apply migrations + seed demo data at startup (dev MVP convenience).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DatabaseInitializer.InitializeAsync(db);
}

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseAuthentication();
// Must run before UseAuthorization: a validated HMAC assertion marks the cookie-less
// in-process bridge request authenticated (otherwise the fallback policy 401s it).
app.UseMiddleware<InternalAssertionMiddleware>();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRateLimiter();
app.MapControllers()
    .RequireRateLimiting("general");
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

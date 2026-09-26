using System.Threading.RateLimiting;
using AI.TeachingAssistant.Api.Middleware;
using AI.TeachingAssistant.Infrastructure;
using AI.TeachingAssistant.Infrastructure.Persistence;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Basic request limit for AI usage (spec §19/§20).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("ai", context =>
        RateLimitPartition.GetFixedWindowLimiter("ai", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    options.AddPolicy("general", context =>
        RateLimitPartition.GetFixedWindowLimiter("general", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 200,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddTeachingAssistantInfrastructure(builder.Configuration);

var app = builder.Build();

// Apply migrations + seed demo data at startup (dev MVP convenience).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DatabaseInitializer.InitializeAsync(db);
}

app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRateLimiter();
app.MapControllers()
    .RequireRateLimiting("general");

app.Run();

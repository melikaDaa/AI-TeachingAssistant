using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Application.Interfaces;
using AI.TeachingAssistant.Application.Services;
using AI.TeachingAssistant.Infrastructure.Ai;
using AI.TeachingAssistant.Infrastructure.Persistence;
using AI.TeachingAssistant.Infrastructure.Repositories;
using AI.TeachingAssistant.Infrastructure.Generation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AI.TeachingAssistant.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers database, repositories, application services, AI provider and the background worker.</summary>
    public static IServiceCollection AddTeachingAssistantInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // ---------- Database (SQLite for MVP; Postgres later via the same DbContext) ----------
        var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=madreseai.db";
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

        // ---------- AI provider (mock by default; xKiro via OpenAI-compatible API) ----------
        var aiOptions = new OpenAiCompatibleOptions();
        configuration.GetSection(OpenAiCompatibleOptions.SectionName).Bind(aiOptions);

        // Environment variables win over appsettings (spec Step 3 §5):
        //   AI_PROVIDER / AI_API_KEY / AI_BASE_URL / AI_MODEL  — generic
        //   XKIRO_API_KEY                                      — xKiro-specific alias
        aiOptions.ApiKey = FirstNonEmpty(
            Environment.GetEnvironmentVariable("AI_API_KEY"),
            Environment.GetEnvironmentVariable("XKIRO_API_KEY"),
            aiOptions.ApiKey);
        aiOptions.BaseUrl = FirstNonEmpty(Environment.GetEnvironmentVariable("AI_BASE_URL"), aiOptions.BaseUrl);
        aiOptions.Model = FirstNonEmpty(Environment.GetEnvironmentVariable("AI_MODEL"), aiOptions.Model);
        var provider = FirstNonEmpty(
            Environment.GetEnvironmentVariable("AI_PROVIDER"),
            configuration["AI:Provider"],
            "mock");

        services.AddSingleton(aiOptions);
        // "xkiro" and "openai" both mean: real provider over the OpenAI-compatible client.
        // Anything else (incl. "mock") stays on the offline mock so development needs no key.
        if (provider.Equals("xkiro", StringComparison.OrdinalIgnoreCase) ||
            provider.Equals("openai", StringComparison.OrdinalIgnoreCase))
        {
            // The real provider needs its BaseUrl (config/env) wired onto the HttpClient —
            // chat/completions is called as a relative URI against this base address.
            services.AddHttpClient<IAIProvider, OpenAiCompatibleProvider>((sp, http) =>
            {
                var opts = sp.GetRequiredService<OpenAiCompatibleOptions>();
                // Trailing slash is required: base "…/v1" + relative "chat/completions"
                // would otherwise resolve to "…/chat/completions" (dropping /v1 → 404).
                http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
                http.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            });
        }
        else
        {
            services.AddSingleton<IAIProvider, MockAIProvider>();
        }

        // ---------- Repositories ----------
        services.AddScoped<ITeacherRepository, TeacherRepository>();
        services.AddScoped<ILessonRepository, LessonRepository>();
        services.AddScoped<ITeachingPackageRepository, TeachingPackageRepository>();

        // ---------- Application services ----------
        services.AddScoped<LessonService>();
        services.AddScoped<TeacherService>();
        services.AddScoped<PackageSectionService>();
        services.AddScoped<SectionGenerationService>();
        services.AddScoped<GenerationCoordinator>();

        // ---------- Background generation ----------
        services.AddSingleton<GenerationJobQueue>();
        services.AddSingleton<IGenerationJobQueue>(sp => sp.GetRequiredService<GenerationJobQueue>());
        services.AddHostedService<GenerationJobWorker>();

        return services;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}

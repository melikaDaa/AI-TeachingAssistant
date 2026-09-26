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

        // ---------- AI provider (mock by default; OpenAI-compatible when AI_PROVIDER=openai) ----------
        var aiOptions = new OpenAiCompatibleOptions();
        configuration.GetSection(OpenAiCompatibleOptions.SectionName).Bind(aiOptions);

        // Spec §27: AI_PROVIDER / AI_API_KEY / AI_BASE_URL / AI_MODEL environment variables win.
        aiOptions.ApiKey = FirstNonEmpty(Environment.GetEnvironmentVariable("AI_API_KEY"), aiOptions.ApiKey);
        aiOptions.BaseUrl = FirstNonEmpty(Environment.GetEnvironmentVariable("AI_BASE_URL"), aiOptions.BaseUrl);
        aiOptions.Model = FirstNonEmpty(Environment.GetEnvironmentVariable("AI_MODEL"), aiOptions.Model);
        var provider = FirstNonEmpty(
            Environment.GetEnvironmentVariable("AI_PROVIDER"),
            configuration["AI:Provider"],
            "mock");

        services.AddSingleton(aiOptions);
        if (provider.Equals("openai", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IAIProvider, OpenAiCompatibleProvider>();
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

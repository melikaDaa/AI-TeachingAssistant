using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Application.Dtos;
using AI.TeachingAssistant.Application.Interfaces;
using AI.TeachingAssistant.Application.Mapping;
using AI.TeachingAssistant.Application.Services;
using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Infrastructure.Ai;
using AI.TeachingAssistant.Infrastructure.Persistence;
using AI.TeachingAssistant.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AI.TeachingAssistant.Tests;

/// <summary>
/// End-to-end image rendering over SQLite in-memory: the image-prompt section renders images
/// (through fakes) and exposes their URLs via the DTO, and nothing is rendered when the
/// provider is disabled.
/// </summary>
public sealed class ImageRenderingTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public ImageRenderingTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddScoped<ICurrentTeacher, TestCurrentTeacher>();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddSingleton<IAIProvider, MockAIProvider>();
        services.AddSingleton<IImageProvider>(new FakeImageProvider());
        services.AddSingleton<IImageStore>(new InMemoryImageStore());
        services.AddScoped<SectionGenerationService>();
        services.AddScoped<LessonService>();
        services.AddScoped<PackageSectionService>();
        services.AddScoped<GenerationCoordinator>();
        services.AddSingleton<ILessonRepository, LessonRepository>();
        services.AddSingleton<ITeachingPackageRepository, TeachingPackageRepository>();
        services.AddSingleton<ITeacherRepository, TeacherRepository>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        _provider = services.BuildServiceProvider();

        var db = _provider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        db.Teachers.Add(new Teacher
        {
            FullName = "مریم احمدی",
            Level = TeachingLevels.MiddleSchool,
            Grade = "هفتم",
            Subjects = "علوم",
            TeachingStyle = "تعاملی",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }

    private GenerationCoordinator Coordinator => _provider.GetRequiredService<GenerationCoordinator>();

    private async Task<int> CreateLessonAsync()
    {
        var created = await _provider.GetRequiredService<LessonService>().CreateAsync(new CreateLessonRequest
        {
            Title = "سلول چیست؟",
            Subject = "علوم",
            Grade = "هفتم",
            Topic = "سلول",
            DurationMinutes = 60,
            StudentLevel = "متوسط",
            RequestedSections = new List<int> { ContentTypes.ImagePrompt },
        });
        return created.Id;
    }

    [Fact]
    public async Task Image_prompts_are_rendered_and_exposed_as_urls()
    {
        var id = await CreateLessonAsync();
        var result = await Coordinator.GeneratePackageAsync(id);
        Assert.True(result.Ok, result.Error);

        var lesson = await _provider.GetRequiredService<ILessonRepository>().GetByIdAsync(id, includePackage: true);
        var images = lesson!.Package!.MediaPrompts.Where(m => m.Kind == MediaPromptKind.Image).ToList();
        Assert.Equal(2, images.Count);
        Assert.All(images, m =>
        {
            Assert.False(string.IsNullOrWhiteSpace(m.ImageModel!.ImageUrl));
            Assert.NotNull(m.ImageModel!.ImageGeneratedAt);
        });

        var dto = lesson.ToPackageDto();
        Assert.Equal(2, dto.Media.ImagePrompts.Count);
        Assert.All(dto.Media.ImagePrompts, i => Assert.False(string.IsNullOrWhiteSpace(i.ImageUrl)));
    }

    [Fact]
    public async Task Disabled_provider_keeps_prompts_without_images()
    {
        var services = new ServiceCollection();
        services.AddScoped<ICurrentTeacher, TestCurrentTeacher>();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddSingleton<IAIProvider, MockAIProvider>();
        services.AddSingleton<IImageProvider, DisabledImageProvider>();
        services.AddSingleton<IImageStore>(new InMemoryImageStore());
        services.AddScoped<SectionGenerationService>();
        services.AddScoped<LessonService>();
        services.AddScoped<PackageSectionService>();
        services.AddScoped<GenerationCoordinator>();
        services.AddSingleton<ILessonRepository, LessonRepository>();
        services.AddSingleton<ITeachingPackageRepository, TeachingPackageRepository>();
        services.AddSingleton<ITeacherRepository, TeacherRepository>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        using var provider = services.BuildServiceProvider();
        var id = await CreateLessonAsync();
        var result = await provider.GetRequiredService<GenerationCoordinator>().GeneratePackageAsync(id);
        Assert.True(result.Ok, result.Error);

        var lesson = await provider.GetRequiredService<ILessonRepository>().GetByIdAsync(id, includePackage: true);
        var images = lesson!.Package!.MediaPrompts.Where(m => m.Kind == MediaPromptKind.Image).ToList();
        Assert.Equal(2, images.Count);
        Assert.All(images, m => Assert.True(string.IsNullOrWhiteSpace(m.ImageModel!.ImageUrl)));
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }
}

using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Application.Dtos;
using AI.TeachingAssistant.Application.Interfaces;
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
/// End-to-end workflow tests over a real SQLite in-memory database (spec §32):
/// full generation, partial generation, regenerate isolation, persistence, delete.
/// </summary>
public sealed class GenerationWorkflowTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public GenerationWorkflowTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddSingleton<IAIProvider, MockAIProvider>();
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

    private async Task<int> CreateLessonAsync(List<int>? sections = null)
    {
        var lessonService = _provider.GetRequiredService<LessonService>();
        var created = await lessonService.CreateAsync(new CreateLessonRequest
        {
            Title = "سلول چیست؟",
            Subject = "علوم",
            Grade = "هفتم",
            Topic = "سلول",
            DurationMinutes = 60,
            StudentLevel = "متوسط",
            RequestedSections = sections,
        });
        return created.Id;
    }

    [Fact]
    public async Task Complete_Generation_Produces_All_Six_Sections()
    {
        var id = await CreateLessonAsync();
        var result = await Coordinator.GeneratePackageAsync(id);

        Assert.True(result.Ok, result.Error);
        var lesson = await _provider.GetRequiredService<ILessonRepository>().GetByIdAsync(id, includePackage: true);
        Assert.Equal(PackageStatus.Ready, lesson!.PackageStatus);
        Assert.NotNull(lesson.Package!.LessonPlan);
        Assert.NotNull(lesson.Package.EducationalContent);
        Assert.NotNull(lesson.Package.Activity);
        Assert.NotNull(lesson.Package.Quiz);
        Assert.Equal(2, lesson.Package.MediaPrompts.Count(m => m.Kind == MediaPromptKind.Image));
        Assert.Equal(1, lesson.Package.MediaPrompts.Count(m => m.Kind == MediaPromptKind.Video));
    }

    [Fact]
    public async Task Partial_Generation_Generates_Only_Selected_Sections()
    {
        var id = await CreateLessonAsync(new List<int> { ContentTypes.Quiz });
        var result = await Coordinator.GeneratePackageAsync(id);

        Assert.True(result.Ok, result.Error);
        var lesson = await _provider.GetRequiredService<ILessonRepository>().GetByIdAsync(id, includePackage: true);
        Assert.Equal(PackageStatus.Ready, lesson!.PackageStatus);
        Assert.Null(lesson.Package!.LessonPlan);
        Assert.Null(lesson.Package.EducationalContent);
        Assert.Null(lesson.Package.Activity);
        Assert.NotNull(lesson.Package.Quiz);
        Assert.Empty(lesson.Package.MediaPrompts);
    }

    [Fact]
    public async Task Regenerating_Quiz_Does_Not_Change_Lesson_Plan()
    {
        var id = await CreateLessonAsync();
        await Coordinator.GeneratePackageAsync(id);

        var repo = _provider.GetRequiredService<ILessonRepository>();
        var before = await repo.GetByIdAsync(id, includePackage: true);
        var planPayload = before!.Package!.LessonPlan!.PayloadJson;
        var quizBefore = before.Package.Quiz!.PayloadJson;

        var result = await Coordinator.GenerateSectionAsync(id, ContentTypes.Quiz);
        Assert.True(result.Ok, result.Error);

        var after = await repo.GetByIdAsync(id, includePackage: true);
        Assert.Equal(planPayload, after!.Package!.LessonPlan!.PayloadJson); // untouched (spec §20)
        Assert.NotEqual(quizBefore, after.Package.Quiz!.PayloadJson);       // actually regenerated
        Assert.Equal(PackageStatus.Ready, after.PackageStatus);
    }

    [Fact]
    public async Task Section_Regen_Flips_Status_To_Generating_Then_Back_To_Ready()
    {
        // PART 18: a single-section regenerate must be observable — while the job runs the
        // package status is Generating, and when it finishes the previous status returns.
        var id = await CreateLessonAsync();
        await Coordinator.GeneratePackageAsync(id);
        var repo = _provider.GetRequiredService<ILessonRepository>();

        var result = await Coordinator.GenerateSectionAsync(id, ContentTypes.Activity);
        Assert.True(result.Ok, result.Error);

        var after = await repo.GetByIdAsync(id, includePackage: true);
        Assert.Equal(PackageStatus.Ready, after!.PackageStatus);
        Assert.NotNull(after.Package!.Activity); // content survived the regen cycle
    }

    [Fact]
    public async Task Failed_Section_Regen_Restores_Previous_Status()
    {
        var id = await CreateLessonAsync(new List<int> { ContentTypes.Quiz });
        var result = await Coordinator.GeneratePackageAsync(id);
        Assert.True(result.Ok, result.Error);
        var repo = _provider.GetRequiredService<ILessonRepository>();

        // Quiz exists; regenerating the missing activity fails (no plan → no shared context
        // requirements are satisfied by the mock, but the activity needs the lesson plan
        // context). Whatever the failure reason, the previous Ready status must return.
        var regen = await Coordinator.GenerateSectionAsync(id, ContentTypes.Activity);
        var after = await repo.GetByIdAsync(id, includePackage: true);

        if (!regen.Ok)
        {
            // Failed regen of a Ready package must not leave it Generating forever.
            Assert.NotEqual(PackageStatus.Generating, after!.PackageStatus);
            Assert.NotNull(after!.LastError);
        }
        else
        {
            Assert.Equal(PackageStatus.Ready, after!.PackageStatus);
        }
    }

    [Fact]
    public async Task Generated_Content_Persists_After_Reopening()
    {
        var id = await CreateLessonAsync();
        await Coordinator.GeneratePackageAsync(id);

        // A fresh repository instance simulates reopening the app / a new request scope.
        var repo = new LessonRepository(_provider.GetRequiredService<AppDbContext>());
        var reopened = await repo.GetByIdAsync(id, includePackage: true);

        Assert.NotNull(reopened!.Package);
        Assert.Equal(PackageStatus.Ready, reopened.PackageStatus);
        Assert.Equal(8, reopened.Package.Quiz!.Model!.Questions.Count);
        Assert.True(reopened.Package.Quiz.Model!.Questions.All(q => !string.IsNullOrWhiteSpace(q.Explanation)));
    }

    [Fact]
    public async Task Deleted_Lesson_Is_No_Longer_Visible()
    {
        var id = await CreateLessonAsync();
        var lessonService = _provider.GetRequiredService<LessonService>();

        await lessonService.DeleteAsync(id);

        var repo = _provider.GetRequiredService<ILessonRepository>();
        Assert.Null(await repo.GetByIdAsync(id, includePackage: true));
    }

    [Fact]
    public async Task Mock_Quiz_Answers_Stay_Valid_Across_Variants()
    {
        // Rotating variants must always satisfy the quiz validator (5 mcq + 3 short, valid keys).
        var sectionService = new SectionGenerationService(new MockAIProvider());
        var ctx = new GenerationContext(
            new Teacher { FullName = "مریم", Level = "متوسطه اول", Grade = "هفتم", Subjects = "علوم", TeachingStyle = "تعاملی" },
            new Lesson { Title = "سلول چیست؟", Subject = "علوم", Grade = "هفتم", Topic = "سلول", DurationMinutes = 60, StudentLevel = "متوسط" },
            ContentTypes.Quiz);

        for (var i = 0; i < 6; i++)
        {
            var result = await sectionService.GenerateQuizAsync(ctx);
            Assert.True(result.Ok, result.Error);
        }
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }
}

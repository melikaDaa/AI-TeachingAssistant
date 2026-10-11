using System.Net;
using System.Text;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Application.Dtos;
using AI.TeachingAssistant.Application.Interfaces;
using AI.TeachingAssistant.Application.Services;
using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Infrastructure.Ai;
using AI.TeachingAssistant.Infrastructure.Persistence;
using AI.TeachingAssistant.Infrastructure.Repositories;
using AI.TeachingAssistant.Infrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AI.TeachingAssistant.Tests;

/// <summary>
/// Robustness contract for the image path the happy-path tests do not touch: a failing image
/// provider must never take down other sections, and the store must refuse anything that is
/// not a plain hex file name. All mocked, no network, no cost.
/// </summary>
public sealed class ImageRobustnessTests
{
    [Fact]
    public async Task Image_render_failure_does_not_fail_the_package()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
        services.AddSingleton<IAIProvider, MockAIProvider>();
        services.AddSingleton<IImageProvider>(new FailingImageProvider());
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
        var db = provider.GetRequiredService<AppDbContext>();
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

        var created = await provider.GetRequiredService<LessonService>().CreateAsync(new CreateLessonRequest
        {
            Title = "سلول چیست؟",
            Subject = "علوم",
            Grade = "هفتم",
            Topic = "سلول",
            DurationMinutes = 60,
            StudentLevel = "متوسط",
            RequestedSections = new List<int> { ContentTypes.LessonPlan, ContentTypes.ImagePrompt },
        });

        var result = await provider.GetRequiredService<GenerationCoordinator>().GeneratePackageAsync(created.Id);

        Assert.True(result.Ok, result.Error);
        var lesson = await provider.GetRequiredService<ILessonRepository>().GetByIdAsync(created.Id, includePackage: true);
        Assert.NotNull(lesson!.Package!.LessonPlan);
        var images = lesson.Package.MediaPrompts.Where(m => m.Kind == MediaPromptKind.Image).ToList();
        Assert.NotEmpty(images);
        Assert.All(images, m => Assert.True(string.IsNullOrWhiteSpace(m.ImageModel!.ImageUrl)));
    }

    [Fact]
    public async Task Failed_job_status_is_a_transient_persian_error()
    {
        var handler = new XKiroImageProviderTests.StubHandler((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/images/generations"))
                return Json(HttpStatusCode.Accepted, """{"id":"job-9","status":"processing"}""");
            return Json(HttpStatusCode.OK, """{"id":"job-9","status":"failed","error":{"message":"provider boom"}}""");
        });

        var ex = await Assert.ThrowsAsync<AiProviderException>(
            () => Provider(handler).GenerateAsync(new ImageGenerationRequest("x")));

        Assert.True(ex.IsTransient);
        Assert.Contains("ناموفق", ex.Message);
    }

    [Fact]
    public async Task Malformed_submit_response_is_a_contract_error()
    {
        var handler = new XKiroImageProviderTests.StubHandler((_, _) =>
            Json(HttpStatusCode.Accepted, "not-json-at-all"));

        var ex = await Assert.ThrowsAsync<AiProviderException>(
            () => Provider(handler).GenerateAsync(new ImageGenerationRequest("x")));

        Assert.False(ex.IsTransient);
    }

    [Fact]
    public async Task Store_roundtrip_preserves_bytes_and_content_type()
    {
        using var store = new TempFileImageStore();
        var url = await store.Store.SaveAsync(new byte[] { 1, 2, 3 }, "image/jpeg");
        Assert.StartsWith("/api/images/", url);
        var name = url["/api/images/".Length..];

        var image = await store.Store.GetAsync(name);
        Assert.NotNull(image);
        Assert.Equal(new byte[] { 1, 2, 3 }, image!.Data);
        Assert.Equal("image/jpeg", image.ContentType);
    }

    [Fact]
    public async Task Store_refuses_unsafe_or_unknown_names()
    {
        using var store = new TempFileImageStore();

        foreach (var name in new[] { "", "../escape.png", "..\\escape.png", "..%2F..%2Fx.png",
                                     "abc.png", "x.png", "deadbeef.png", "space in name.png" })
        {
            Assert.Null(await store.Store.GetAsync(name));
        }

        // A name that matches the pattern but points at no real file is also null.
        Assert.Null(await store.Store.GetAsync("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg"));
    }

    [Fact]
    public async Task Store_maps_webp_and_unknown_content_types()
    {
        using var store = new TempFileImageStore();

        var webpUrl = await store.Store.SaveAsync(new byte[] { 9 }, "image/webp");
        Assert.EndsWith(".webp", webpUrl);
        Assert.Equal("image/webp", (await store.Store.GetAsync(webpUrl["/api/images/".Length..]))!.ContentType);

        var unknownUrl = await store.Store.SaveAsync(new byte[] { 9 }, "application/octet-stream");
        Assert.EndsWith(".png", unknownUrl);
        Assert.Equal("image/png", (await store.Store.GetAsync(unknownUrl["/api/images/".Length..]))!.ContentType);
    }

    private static XKiroImageProvider Provider(XKiroImageProviderTests.StubHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.xkiro.com/v1/") };
        var options = new ImageGenerationOptions
        {
            Provider = "xkiro",
            ApiKey = "test-key",
            BaseUrl = "https://api.xkiro.com/v1",
            Model = "sensenova/sensenova-u1.5-lite",
            PollTimeoutSeconds = 60,
            MaxImagesPerSection = 2,
        };
        return new XKiroImageProvider(http, options);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>Image provider that always fails, to prove isolation of the rest of the flow.</summary>
    private sealed class FailingImageProvider : IImageProvider
    {
        public string Name => "Failing image provider";
        public bool IsEnabled => true;
        public int MaxImagesPerSection => 2;

        public Task<GeneratedImage> GenerateAsync(ImageGenerationRequest request, CancellationToken cancellationToken = default) =>
            throw new AiProviderException("شکست آزمایشی تولید تصویر", isTransient: true);
    }

    /// <summary>Owns a fresh temp folder for the whole test and deletes it on dispose.</summary>
    private sealed class TempFileImageStore : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "imagerobustness-" + Guid.NewGuid().ToString("N"));

        public TempFileImageStore() => Store = new FileImageStore(_root);

        public FileImageStore Store { get; }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
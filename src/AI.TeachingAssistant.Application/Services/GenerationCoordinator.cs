using AI.TeachingAssistant.Application.Ai;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Application.Interfaces;
using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AI.TeachingAssistant.Application.Services;

/// <summary>
/// Orchestrates AI generation. Every section is generated and persisted independently, so a
/// failure or a regenerate request for one section never touches the others.
/// </summary>
public class GenerationCoordinator
{
    private readonly SectionGenerationService _sections;
    private readonly ILessonRepository _lessons;
    private readonly ITeachingPackageRepository _packages;
    private readonly ITeacherRepository _teachers;
    private readonly ILogger<GenerationCoordinator> _logger;

    public GenerationCoordinator(
        SectionGenerationService sections,
        ILessonRepository lessons,
        ITeachingPackageRepository packages,
        ITeacherRepository teachers,
        ILogger<GenerationCoordinator> logger)
    {
        _sections = sections;
        _lessons = lessons;
        _packages = packages;
        _teachers = teachers;
        _logger = logger;
    }

    /// <summary>Generates one section (used by the per-section regenerate endpoint and the job worker).</summary>
    public async Task<Result<TeachingPackage>> GenerateSectionAsync(int lessonId, int sectionType, CancellationToken ct = default)
    {
        var lesson = await _lessons.GetByIdAsync(lessonId, includePackage: true, ct);
        if (lesson is null) return Result<TeachingPackage>.Failure("درس یافت نشد.");

        var teacher = await _teachers.GetDefaultAsync(ct);
        if (teacher is null)
            return Result<TeachingPackage>.Failure("ابتدا پروفایل معلم را تکمیل کنید.");

        var package = lesson.Package ?? await CreatePackageAsync(lesson, ct);
        var ctx = new GenerationContext(teacher, lesson, sectionType);

        Result<bool> sectionResult = sectionType switch
        {
            ContentTypes.LessonPlan => await GenerateAndStoreAsync(package, async p =>
            {
                var r = await _sections.GenerateLessonPlanAsync(ctx, ct);
                p.LessonPlan = new LessonPlan { TeachingPackageId = p.Id, PayloadJson = PayloadJson.Serialize(r.Value!), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
                return true;
            }),
            ContentTypes.EducationalContent => await GenerateAndStoreAsync(package, async p =>
            {
                var r = await _sections.GenerateEducationalContentAsync(ctx, ct);
                p.EducationalContent = new EducationalContent { TeachingPackageId = p.Id, PayloadJson = PayloadJson.Serialize(r.Value!), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
                return true;
            }),
            ContentTypes.Activity => await GenerateAndStoreAsync(package, async p =>
            {
                var r = await _sections.GenerateActivityAsync(ctx, ct);
                p.Activity = new ClassroomActivity { TeachingPackageId = p.Id, PayloadJson = PayloadJson.Serialize(r.Value!), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
                return true;
            }),
            ContentTypes.Quiz => await GenerateAndStoreAsync(package, async p =>
            {
                var r = await _sections.GenerateQuizAsync(ctx, ct);
                p.Quiz = new Quiz { TeachingPackageId = p.Id, PayloadJson = PayloadJson.Serialize(r.Value!), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
                return true;
            }),
            ContentTypes.ImagePrompt => await GenerateAndStoreAsync(package, async p =>
            {
                var r = await _sections.GenerateImagePromptsAsync(ctx, ct);
                p.MediaPrompts.RemoveAll(m => m.Kind == MediaPromptKind.Image);
                var i = 1;
                foreach (var img in r.Value!)
                {
                    p.MediaPrompts.Add(new MediaPrompt
                    {
                        TeachingPackageId = p.Id,
                        Kind = MediaPromptKind.Image,
                        Title = $"تصویر {ToPersianDigits(i)}",
                        PayloadJson = PayloadJson.Serialize(img),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    });
                    i++;
                }
                return true;
            }),
            ContentTypes.VideoPrompt => await GenerateAndStoreAsync(package, async p =>
            {
                var r = await _sections.GenerateVideoPromptAsync(ctx, ct);
                p.MediaPrompts.RemoveAll(m => m.Kind == MediaPromptKind.Video);
                p.MediaPrompts.Add(new MediaPrompt
                {
                    TeachingPackageId = p.Id,
                    Kind = MediaPromptKind.Video,
                    Title = "ویدئوی آموزشی",
                    PayloadJson = PayloadJson.Serialize(r.Value!),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
                return true;
            }),
            _ => Result<bool>.Failure("نوع محتوای نامعتبر است."),
        };

        if (!sectionResult.Ok)
        {
            lesson.LastError = sectionResult.Error;
            await _lessons.UpdateAsync(lesson, ct);
            _logger.LogWarning("Section {Section} generation failed for lesson {LessonId}: {Error}", sectionType, lessonId, sectionResult.Error);
            return Result<TeachingPackage>.Failure(sectionResult.Error!);
        }

        lesson.LastError = null;
        MarkSectionCompleted(lesson, package, sectionType);
        await _lessons.UpdateAsync(lesson, ct);
        await _packages.UpdateAsync(package, ct);

        _logger.LogInformation("Section {Section} generated for lesson {LessonId}", sectionType, lessonId);
        return Result<TeachingPackage>.Success(package);
    }

    /// <summary>Generates all requested sections sequentially, persisting each one as it completes.</summary>
    public async Task<Result<TeachingPackage>> GeneratePackageAsync(int lessonId, CancellationToken ct = default)
    {
        var lesson = await _lessons.GetByIdAsync(lessonId, includePackage: true, ct);
        if (lesson is null) return Result<TeachingPackage>.Failure("درس یافت نشد.");

        var requested = ParseSections(lesson.RequestedSections);
        lesson.PackageStatus = PackageStatus.Generating;
        lesson.LastError = null;
        await _lessons.UpdateAsync(lesson, ct);

        var firstError = (string?)null;
        foreach (var section in requested)
        {
            ct.ThrowIfCancellationRequested();
            var result = await GenerateSectionAsync(lessonId, section, ct);
            if (!result.Ok)
            {
                firstError ??= result.Error;
            }
        }

        if (firstError is not null)
        {
            var current = await _lessons.GetByIdAsync(lessonId, includePackage: true, ct);
            if (current is not null)
            {
                current.PackageStatus = PackageStatus.Failed;
                current.LastError = firstError;
                await _lessons.UpdateAsync(current, ct);
            }
            return Result<TeachingPackage>.Failure(firstError);
        }

        var done = await _lessons.GetByIdAsync(lessonId, includePackage: true, ct);
        if (done is not null)
        {
            done.PackageStatus = PackageStatus.Ready;
            done.LastError = null;
            await _lessons.UpdateAsync(done, ct);
        }

        var package = done?.Package;
        return package is null
            ? Result<TeachingPackage>.Failure("محتوای تولیدشده ذخیره نشد.")
            : Result<TeachingPackage>.Success(package);
    }

    private async Task<Result<bool>> GenerateAndStoreAsync(TeachingPackage package, Func<TeachingPackage, Task<bool>> store)
    {
        await store(package);
        return Result<bool>.Success(true);
    }

    private async Task<TeachingPackage> CreatePackageAsync(Lesson lesson, CancellationToken ct)
    {
        var package = new TeachingPackage
        {
            LessonId = lesson.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await _packages.AddAsync(package, ct);
        lesson.Package = package;
        return package;
    }

    private static void MarkSectionCompleted(Lesson lesson, TeachingPackage package, int sectionType)
    {
        var code = sectionType.ToString();
        if (!package.CompletedSections.Contains(code))
            package.CompletedSections = package.CompletedSections.Append(code).ToArray();

        if (lesson.PackageStatus is PackageStatus.Generating or PackageStatus.Draft or PackageStatus.Failed)
        {
            lesson.PackageStatus = ParseSections(lesson.RequestedSections)
                .All(s => package.CompletedSections.Contains(s.ToString()))
                ? PackageStatus.Ready
                : PackageStatus.Generating;
        }

        // A manual regenerate of a missing section effectively runs a mini-package run:
        // if everything requested is now present, the package is Ready.
        if (lesson.PackageStatus == PackageStatus.Generating && package.CompletedSections.Length == 0)
            lesson.PackageStatus = PackageStatus.Draft;

        package.UpdatedAt = DateTime.UtcNow;
    }

    internal static List<int> ParseSections(string stored)
    {
        var parsed = stored
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? n : -1)
            .Where(n => n > 0)
            .Distinct()
            .ToList();
        return parsed.Count == 0 ? ContentTypes.AllSections.ToList() : parsed;
    }

    private static string ToPersianDigits(int value)
    {
        var digits = "۰۱۲۳۴۵۶۷۸۹";
        return string.Concat(value.ToString().Select(d => digits[d - '0']));
    }
}

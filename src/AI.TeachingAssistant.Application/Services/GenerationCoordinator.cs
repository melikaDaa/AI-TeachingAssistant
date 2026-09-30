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

    /// <summary>
    /// Generates one section. Used by the per-section regenerate endpoint/job (default) and
    /// by <see cref="GeneratePackageAsync"/> (which passes <c>isSingleSectionJob: false</c>
    /// so the package run itself governs the lesson status).
    /// </summary>
    public async Task<Result<TeachingPackage>> GenerateSectionAsync(int lessonId, int sectionType, CancellationToken ct = default, bool isSingleSectionJob = true)
    {
        var lesson = await _lessons.GetByIdAsync(lessonId, includePackage: true, ct);
        if (lesson is null) return Result<TeachingPackage>.Failure("درس یافت نشد.");

        var teacher = await _teachers.GetDefaultAsync(ct);
        if (teacher is null)
            return Result<TeachingPackage>.Failure("ابتدا پروفایل معلم را تکمیل کنید.");

        var package = lesson.Package ?? await CreatePackageAsync(lesson, ct);

        // Observable regeneration (PART 18): the lesson must read as Generating before the
        // AI call starts, so pollers never mistake a regenerate for an instant no-op. The
        // controller usually flipped it before enqueueing; this also covers direct callers.
        if (lesson.PackageStatus is not PackageStatus.Generating)
        {
            lesson.PackageStatus = PackageStatus.Generating;
            await _lessons.UpdateAsync(lesson, ct);
        }

        var ctx = new GenerationContext(
            teacher,
            lesson,
            sectionType,
            BuildSharedContext(package, sectionType),
            BuildRegeneration(package, sectionType));

        Result<bool> sectionResult;
        try
        {
            sectionResult = sectionType switch
            {
                ContentTypes.LessonPlan => await GenerateAndStoreAsync(package, async p =>
                {
                    var r = await _sections.GenerateLessonPlanAsync(ctx, ct);
                    if (!r.Ok) return Result<string?>.Failure(r.Error!);
                    p.LessonPlan = new LessonPlan { TeachingPackageId = p.Id, PayloadJson = PayloadJson.Serialize(r.Value!), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
                    return Result<string?>.Success(null);
                }),
                ContentTypes.EducationalContent => await GenerateAndStoreAsync(package, async p =>
                {
                    var r = await _sections.GenerateEducationalContentAsync(ctx, ct);
                    if (!r.Ok) return Result<string?>.Failure(r.Error!);
                    p.EducationalContent = new EducationalContent { TeachingPackageId = p.Id, PayloadJson = PayloadJson.Serialize(r.Value!), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
                    return Result<string?>.Success(null);
                }),
                ContentTypes.Activity => await GenerateAndStoreAsync(package, async p =>
                {
                    var r = await _sections.GenerateActivityAsync(ctx, ct);
                    if (!r.Ok) return Result<string?>.Failure(r.Error!);
                    p.Activity = new ClassroomActivity { TeachingPackageId = p.Id, PayloadJson = PayloadJson.Serialize(r.Value!), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
                    return Result<string?>.Success(null);
                }),
                ContentTypes.Quiz => await GenerateAndStoreAsync(package, async p =>
                {
                    var r = await _sections.GenerateQuizAsync(ctx, ct);
                    if (!r.Ok) return Result<string?>.Failure(r.Error!);
                    p.Quiz = new Quiz { TeachingPackageId = p.Id, PayloadJson = PayloadJson.Serialize(r.Value!), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
                    return Result<string?>.Success(null);
                }),
                ContentTypes.ImagePrompt => await GenerateAndStoreAsync(package, async p =>
                {
                    var r = await _sections.GenerateImagePromptsAsync(ctx, ct);
                    if (!r.Ok) return Result<string?>.Failure(r.Error!);
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
                    return Result<string?>.Success(null);
                }),
                ContentTypes.VideoPrompt => await GenerateAndStoreAsync(package, async p =>
                {
                    var r = await _sections.GenerateVideoPromptAsync(ctx, ct);
                    if (!r.Ok) return Result<string?>.Failure(r.Error!);
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
                    return Result<string?>.Success(null);
                }),
                _ => Result<bool>.Failure("نوع محتوای نامعتبر است."),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A bug in one section must never kill the whole worker run; record it as a
            // section failure so the teacher sees a Persian message and can retry.
            _logger.LogError(ex, "Unexpected error while generating section {Section} for lesson {LessonId}", sectionType, lessonId);
            sectionResult = Result<bool>.Failure("خطای غیرمنتظره در تولید این بخش رخ داد. لطفاً دوباره تلاش کنید.");
        }

        if (!sectionResult.Ok)
        {
            lesson.LastError = sectionResult.Error;
            if (isSingleSectionJob)
            {
                // A failed standalone regenerate must never leave a forever-"generating"
                // spinner (PART 18): content is unchanged, so a package that is complete
                // without this section stays Ready (error is shown), otherwise Failed.
                var completeWithoutThis = ParseSections(lesson.RequestedSections)
                    .Where(s => s != sectionType)
                    .All(s => package.CompletedSections.Contains(s.ToString()));
                lesson.PackageStatus = completeWithoutThis ? PackageStatus.Ready : PackageStatus.Failed;
            }
            await _lessons.UpdateAsync(lesson, ct);
            _logger.LogWarning("Section {Section} generation failed for lesson {LessonId}: {Error}", sectionType, lessonId, sectionResult.Error);
            return Result<TeachingPackage>.Failure(sectionResult.Error!);
        }

        lesson.LastError = null;
        MarkSectionCompleted(lesson, package, sectionType);
        if (isSingleSectionJob && lesson.PackageStatus == PackageStatus.Generating)
        {
            // Standalone job with requested sections still missing: no job will follow, so
            // the package is idle again. Ready mirrors the partial-generation semantics.
            lesson.PackageStatus = PackageStatus.Ready;
        }
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
            var result = await GenerateSectionAsync(lessonId, section, ct, isSingleSectionJob: false);
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

    /// <summary>
    /// Distills the sections already stored on the package into the compact shared context
    /// (§7/§8): plan → objectives + class flow; content → key concepts. The full earlier
    /// output is never copied into later prompts. Returns null for the lesson plan itself
    /// and when no plan exists yet, so per-section regenerate stays independent.
    /// </summary>
    private static SharedLessonContext? BuildSharedContext(TeachingPackage package, int sectionType)
    {
        if (sectionType == ContentTypes.LessonPlan) return null;

        var planPayload = package.LessonPlan?.PayloadJson;
        var plan = planPayload is null ? null : PayloadJson.Deserialize<LessonPlanModel>(planPayload);
        if (plan is null) return null;

        var shared = SharedLessonContext.FromPlan(plan);
        if (sectionType == ContentTypes.EducationalContent) return shared;

        var contentPayload = package.EducationalContent?.PayloadJson;
        var content = contentPayload is null ? null : PayloadJson.Deserialize<EducationalContentModel>(contentPayload);
        return content is null ? shared : shared.WithContent(content);
    }

    /// <summary>
    /// Wraps the currently stored section payload as a regeneration difference-reference
    /// (PART 2/3). Null on first generation, so normal generation prompts are unchanged.
    /// </summary>
    private static RegenerationInfo? BuildRegeneration(TeachingPackage package, int sectionType) => sectionType switch
    {
        ContentTypes.LessonPlan => RegenerationSupport.Build(package.LessonPlan?.Model, sectionType),
        ContentTypes.EducationalContent => RegenerationSupport.Build(package.EducationalContent?.Model, sectionType),
        ContentTypes.Activity => RegenerationSupport.Build(package.Activity?.Model, sectionType),
        ContentTypes.Quiz => RegenerationSupport.Build(package.Quiz?.Model, sectionType),
        ContentTypes.ImagePrompt => RegenerationSupport.Build(
            package.MediaPrompts.Where(m => m.Kind == MediaPromptKind.Image).Select(m => m.ImageModel).Where(m => m is not null).ToList(), sectionType),
        ContentTypes.VideoPrompt => RegenerationSupport.Build(
            package.MediaPrompts.FirstOrDefault(m => m.Kind == MediaPromptKind.Video)?.VideoModel, sectionType),
        _ => null,
    };

    /// <summary>
    /// Runs the store delegate for one section. A failed generation (Result.Failure) is returned as
    /// a failure here — never dereference <c>r.Value</c> before checking <c>r.Ok</c>.
    /// </summary>
    private async Task<Result<bool>> GenerateAndStoreAsync(TeachingPackage package, Func<TeachingPackage, Task<Result<string?>>> store)
    {
        var result = await store(package);
        return result.Ok
            ? Result<bool>.Success(true)
            : Result<bool>.Failure(result.Error!);
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

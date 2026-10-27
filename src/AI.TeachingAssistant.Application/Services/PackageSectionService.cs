using AI.TeachingAssistant.Application.Ai;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Application.Interfaces;
using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Domain.Models;

namespace AI.TeachingAssistant.Application.Services;

/// <summary>Saves teacher edits for one package section without touching the others.</summary>
public class PackageSectionService
{
    private readonly ILessonRepository _lessons;
    private readonly ITeachingPackageRepository _packages;
    private readonly ICurrentTeacher _currentTeacher;

    public PackageSectionService(ILessonRepository lessons, ITeachingPackageRepository packages, ICurrentTeacher currentTeacher)
    {
        _lessons = lessons;
        _packages = packages;
        _currentTeacher = currentTeacher;
    }

    public async Task<Result<TeachingPackage>> SaveSectionEditAsync(int lessonId, int sectionType, string payloadJson, CancellationToken ct = default)
    {
        var lesson = await _lessons.GetForTeacherAsync(lessonId, _currentTeacher.TeacherId, includePackage: true, ct);
        if (lesson?.Package is null) return Result<TeachingPackage>.Failure("محتوای ذخیره‌شده‌ای برای این درس یافت نشد.");
        var package = lesson.Package;

        var error = sectionType switch
        {
            ContentTypes.LessonPlan => ValidateAndStore<LessonPlanModel>(payloadJson,
                m => AiResponseValidator.Validate(m, lesson.DurationMinutes),
                m => package.LessonPlan = new LessonPlan { TeachingPackageId = package.Id, PayloadJson = PayloadJson.Serialize(m) }),
            ContentTypes.EducationalContent => ValidateAndStore<EducationalContentModel>(payloadJson,
                AiResponseValidator.Validate,
                m => package.EducationalContent = new EducationalContent { TeachingPackageId = package.Id, PayloadJson = PayloadJson.Serialize(m) }),
            ContentTypes.Activity => ValidateAndStore<ActivityModel>(payloadJson,
                m => AiResponseValidator.Validate(m, lesson.DurationMinutes),
                m => package.Activity = new ClassroomActivity { TeachingPackageId = package.Id, PayloadJson = PayloadJson.Serialize(m) }),
            ContentTypes.Quiz => ValidateAndStore<QuizModel>(payloadJson,
                AiResponseValidator.Validate,
                m => package.Quiz = new Quiz { TeachingPackageId = package.Id, PayloadJson = PayloadJson.Serialize(m) }),
            _ => "ویرایش این بخش از این راه پشتیبانی نمی‌شود.",
        };

        if (error is not null) return Result<TeachingPackage>.Failure(error);

        await _packages.UpdateAsync(package, ct);
        return Result<TeachingPackage>.Success(package);
    }

    private static string? ValidateAndStore<TModel>(
        string payloadJson,
        Func<TModel, string?> validate,
        Action<TModel> store)
    {
        var model = PayloadJson.Deserialize<TModel>(payloadJson);
        if (model is null) return "قالب داده ویرایش‌شده معتبر نیست.";
        var error = validate(model);
        if (error is not null) return error;
        store(model);
        return null;
    }
}

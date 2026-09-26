using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Application.Dtos;
using AI.TeachingAssistant.Application.Interfaces;
using AI.TeachingAssistant.Application.Mapping;
using AI.TeachingAssistant.Domain.Entities;

namespace AI.TeachingAssistant.Application.Services;

/// <summary>Lesson use cases: create, list, get, update, delete — with input validation.</summary>
public class LessonService
{
    private static readonly string[] AllowedLevels = { "مبتدی", "متوسط", "پیشرفته" };

    private readonly ILessonRepository _lessons;
    private readonly ITeacherRepository _teachers;

    public LessonService(ILessonRepository lessons, ITeacherRepository teachers)
    {
        _lessons = lessons;
        _teachers = teachers;
    }

    public async Task<LessonSummaryDto> CreateAsync(CreateLessonRequest request, CancellationToken ct = default)
    {
        ValidateLesson(request.Title, request.Subject, request.Grade, request.Topic, request.DurationMinutes, request.StudentLevel);
        var teacher = await _teachers.GetDefaultAsync(ct)
            ?? throw new ValidationException("ابتدا پروفایل معلم را تکمیل کنید.");

        var lesson = new Lesson
        {
            TeacherId = teacher.Id,
            Title = request.Title.Trim(),
            Subject = request.Subject.Trim(),
            Grade = request.Grade.Trim(),
            Topic = request.Topic.Trim(),
            DurationMinutes = request.DurationMinutes,
            StudentLevel = request.StudentLevel.Trim(),
            AdditionalInstructions = request.AdditionalInstructions?.Trim(),
            RequestedSections = NormalizeSections(request.RequestedSections),
            PackageStatus = PackageStatus.Draft,
        };

        await _lessons.AddAsync(lesson, ct);
        return lesson.ToSummaryDto();
    }

    public async Task<List<LessonSummaryDto>> ListAsync(CancellationToken ct = default)
    {
        var teacher = await _teachers.GetDefaultAsync(ct)
            ?? throw new ValidationException("ابتدا پروفایل معلم را تکمیل کنید.");
        var lessons = await _lessons.ListForTeacherAsync(teacher.Id, ct);
        return lessons.Select(l => l.ToSummaryDto()).ToList();
    }

    public async Task<LessonSummaryDto> UpdateAsync(int id, UpdateLessonRequest request, CancellationToken ct = default)
    {
        var lesson = await _lessons.GetByIdAsync(id, includePackage: false, ct)
            ?? throw new ValidationException("درس یافت نشد.");
        ValidateLesson(request.Title, request.Subject, request.Grade, request.Topic, request.DurationMinutes, request.StudentLevel);

        lesson.Title = request.Title.Trim();
        lesson.Subject = request.Subject.Trim();
        lesson.Grade = request.Grade.Trim();
        lesson.Topic = request.Topic.Trim();
        lesson.DurationMinutes = request.DurationMinutes;
        lesson.StudentLevel = request.StudentLevel.Trim();
        lesson.AdditionalInstructions = request.AdditionalInstructions?.Trim();

        await _lessons.UpdateAsync(lesson, ct);
        return lesson.ToSummaryDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        if (!await _lessons.DeleteAsync(id, ct))
            throw new ValidationException("درس یافت نشد.");
    }

    public static void ValidateLesson(string title, string subject, string grade, string topic, int duration, string level)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ValidationException("عنوان درس را وارد کنید.");
        if (title.Trim().Length > 150) throw new ValidationException("عنوان درس نباید بیش از ۱۵۰ نویسه باشد.");
        if (string.IsNullOrWhiteSpace(subject)) throw new ValidationException("نام درس را وارد کنید.");
        if (string.IsNullOrWhiteSpace(grade)) throw new ValidationException("پایه تحصیلی را وارد کنید.");
        if (string.IsNullOrWhiteSpace(topic)) throw new ValidationException("موضوع درس را وارد کنید.");
        if (topic.Trim().Length > 300) throw new ValidationException("موضوع درس نباید بیش از ۳۰۰ نویسه باشد.");
        if (duration < 15 || duration > 240) throw new ValidationException("مدت کلاس باید بین ۱۵ تا ۲۴۰ دقیقه باشد.");
        if (!AllowedLevels.Contains(level)) throw new ValidationException("سطح دانش‌آموزان را انتخاب کنید.");
    }

    public static string NormalizeSections(List<int>? requested)
    {
        if (requested is null || requested.Count == 0)
            return string.Join(',', ContentTypes.AllSections);
        var clean = requested.Where(ContentTypes.AllSections.Contains).Distinct().ToList();
        return clean.Count == 0 ? string.Join(',', ContentTypes.AllSections) : string.Join(',', clean);
    }
}

using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Application.Dtos;
using AI.TeachingAssistant.Application.Interfaces;
using AI.TeachingAssistant.Application.Mapping;

namespace AI.TeachingAssistant.Application.Services;

/// <summary>Teacher profile use cases. MVP: the single (default) teacher.</summary>
public class TeacherService
{
    private readonly ITeacherRepository _teachers;
    private readonly ICurrentTeacher _currentTeacher;

    public TeacherService(ITeacherRepository teachers, ICurrentTeacher currentTeacher)
    {
        _teachers = teachers;
        _currentTeacher = currentTeacher;
    }

    public async Task<TeacherDto> GetAsync(CancellationToken ct = default)
    {
        var teacher = await _teachers.GetByIdAsync(_currentTeacher.TeacherId, ct)
            ?? throw new ValidationException("پروفایل معلم یافت نشد.");
        return teacher.ToDto();
    }

    public async Task<TeacherDto> UpdateAsync(UpdateTeacherRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName)) throw new ValidationException("نام و نام خانوادگی را وارد کنید.");
        if (request.FullName.Trim().Length > 150) throw new ValidationException("نام نباید بیش از ۱۵۰ نویسه باشد.");
        if (string.IsNullOrWhiteSpace(request.Level)) throw new ValidationException("مقطع تحصیلی را انتخاب کنید.");
        if (string.IsNullOrWhiteSpace(request.Grade)) throw new ValidationException("پایه تحصیلی را وارد کنید.");
        if (string.IsNullOrWhiteSpace(request.Subjects)) throw new ValidationException("دروس تدریسی را وارد کنید.");
        if (string.IsNullOrWhiteSpace(request.TeachingStyle)) throw new ValidationException("سبک تدریس را انتخاب کنید.");

        var teacher = await _teachers.GetByIdAsync(_currentTeacher.TeacherId, ct)
            ?? throw new ValidationException("پروفایل معلم یافت نشد.");

        teacher.FullName = request.FullName.Trim();
        teacher.Level = request.Level.Trim();
        teacher.Grade = request.Grade.Trim();
        teacher.Subjects = request.Subjects.Trim();
        teacher.TeachingStyle = request.TeachingStyle.Trim();

        await _teachers.UpdateAsync(teacher, ct);
        return teacher.ToDto();
    }
}

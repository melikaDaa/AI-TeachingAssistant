using AI.TeachingAssistant.Domain.Entities;

namespace AI.TeachingAssistant.Application.Common;

/// <summary>
/// Structured context fed to every prompt builder:
/// teacher profile + lesson info + requested output.
/// </summary>
public sealed record GenerationContext(
    Teacher Teacher,
    Lesson Lesson,
    int SectionType)
{
    public static string LevelHint(string studentLevel) => studentLevel switch
    {
        "مبتدی" => "دانش‌آموزان مبتدی هستند؛ زبان بسیار ساده و مثال‌های عینی به کار ببر.",
        "متوسط" => "دانش‌آموزان سطح متوسط دارند؛ توضیح روان با مثال‌های روزمره.",
        "پیشرفته" => "دانش‌آموزان پیشرفته هستند؛ محتوا را عمیق‌تر و چالشی‌تر بنویس.",
        _ => "سطح دانش‌آموزان را در نظر بگیر و زبان مناسب کلاس به کار ببر.",
    };
}

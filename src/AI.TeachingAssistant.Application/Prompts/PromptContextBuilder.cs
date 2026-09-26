using System.Text;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Domain.Entities;

namespace AI.TeachingAssistant.Application.Prompts;

/// <summary>
/// Shared Persian prompt context: teacher profile + lesson info + student level + teaching style.
/// Every section prompt builder composes its output request on top of this.
/// </summary>
public static class PromptContextBuilder
{
    public static string Build(GenerationContext ctx)
    {
        var l = ctx.Lesson;
        var t = ctx.Teacher;
        var sb = new StringBuilder();
        sb.AppendLine("تو دستیار آموزشی معلمان هستی و محتوای آموزشی به زبان فارسی تولید می‌کنی.");
        sb.AppendLine();
        sb.AppendLine("## مشخصات معلم");
        sb.AppendLine($"- نام: {t.FullName}");
        sb.AppendLine($"- مقطع: {t.Level}");
        sb.AppendLine($"- پایه: {t.Grade}");
        sb.AppendLine($"- دروس: {t.Subjects}");
        sb.AppendLine($"- سبک تدریس: {t.TeachingStyle}");
        sb.AppendLine();
        sb.AppendLine("## مشخصات درس");
        sb.AppendLine($"- عنوان: {l.Title}");
        sb.AppendLine($"- درس: {l.Subject}");
        sb.AppendLine($"- پایه: {l.Grade}");
        sb.AppendLine($"- موضوع: {l.Topic}");
        sb.AppendLine($"- مدت کلاس: {l.DurationMinutes} دقیقه");
        sb.AppendLine($"- سطح دانش‌آموزان: {l.StudentLevel}");
        if (!string.IsNullOrWhiteSpace(l.AdditionalInstructions))
            sb.AppendLine($"- توضیحات تکمیلی معلم: {l.AdditionalInstructions}");
        sb.AppendLine();
        sb.AppendLine(GenerationContext.LevelHint(l.StudentLevel));
        sb.AppendLine($"سبک تدریس معلم «{t.TeachingStyle}» است؛ خروجی را با این سبک هماهنگ کن.");
        return sb.ToString();
    }
}

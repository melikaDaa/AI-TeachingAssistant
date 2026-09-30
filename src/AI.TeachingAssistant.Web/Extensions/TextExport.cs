using System.Text;
using AI.TeachingAssistant.Application.Dtos;

namespace AI.TeachingAssistant.Web.Extensions;

/// <summary>Builds copy-ready plain text for each package section.</summary>
public static class TextExport
{
    public static string LessonPlan(LessonPlanDto p)
    {
        var sb = new StringBuilder("طرح درس\n\nهدف‌های آموزشی:\n");
        foreach (var o in p.LearningObjectives) sb.AppendLine($"• {o}");
        sb.AppendLine("\nوسایل مورد نیاز:");
        foreach (var m in p.RequiredMaterials) sb.AppendLine($"• {m}");
        sb.AppendLine("\nبرنامه زمانی کلاس:");
        foreach (var s in p.Segments)
            sb.AppendLine($"{s.StartMinute.ToPersianDigits()}–{s.EndMinute.ToPersianDigits()} دقیقه: {s.Title} — {s.Description}");
        sb.AppendLine($"\nتکلیف منزل:\n{p.Homework}");
        return sb.ToString();
    }

    public static string EducationalContent(EducationalContentDto c)
    {
        var sb = new StringBuilder("محتوای آموزشی\n\nتوضیح ساده:\n");
        sb.AppendLine(c.SimpleExplanation);
        sb.AppendLine("\nمفاهیم کلیدی:");
        foreach (var k in c.KeyConcepts) sb.AppendLine($"• {k}");
        sb.AppendLine("\nمثال‌ها:");
        foreach (var e in c.Examples) sb.AppendLine($"• {e}");
        sb.AppendLine("\nتصورات غلط رایج:");
        foreach (var m in c.CommonMisconceptions) sb.AppendLine($"• {m}");
        sb.AppendLine($"\nمثال کاربردی:\n{c.RealWorldExample}");
        sb.AppendLine($"\nخلاصه:\n{c.Summary}");
        return sb.ToString();
    }

    public static string Activity(ActivityDto a)
    {
        var sb = new StringBuilder($"فعالیت کلاسی: {a.Title}\n\nهدف: {a.Objective}\n");
        sb.AppendLine("\nوسایل لازم:");
        foreach (var m in a.Materials) sb.AppendLine($"• {m}");
        sb.AppendLine($"\nتعداد دانش‌آموزان: {a.StudentCount}");
        sb.AppendLine($"مدت فعالیت: {a.DurationMinutes.ToPersianDigits()} دقیقه");
        sb.AppendLine("\nدستورالعمل:");
        var i = 1;
        foreach (var step in a.Instructions) sb.AppendLine($"{(i++).ToPersianDigits()}. {step}");
        sb.AppendLine($"\nنتیجه مورد انتظار: {a.ExpectedResult}");
        return sb.ToString();
    }

    public static string Quiz(QuizDto q)
    {
        var sb = new StringBuilder("آزمون\n\nسؤال‌های چهارگزینه‌ای:\n");
        var n = 1;
        foreach (var item in q.Questions.Where(x => x.Type == "mcq"))
        {
            sb.AppendLine($"\n{(n++).ToPersianDigits()}. {item.Text}");
            foreach (var opt in item.Options ?? new List<string>()) sb.AppendLine($"   {opt}");
        }
        sb.AppendLine("\nسؤال‌های کوتاه:");
        n = 1;
        foreach (var item in q.Questions.Where(x => x.Type == "shortAnswer"))
            sb.AppendLine($"\n{(n++).ToPersianDigits()}. {item.Text}");
        sb.AppendLine("\nپاسخ‌نامه:");
        n = 1;
        foreach (var item in q.Questions)
        {
            sb.AppendLine($"{(n++).ToPersianDigits()}. {item.CorrectAnswer}");
            if (!string.IsNullOrWhiteSpace(item.Explanation))
                sb.AppendLine($"   💡 {item.Explanation}");
        }
        return sb.ToString();
    }

    public static string ImagePrompt(ImagePromptDto p)
    {
        var sb = new StringBuilder($"پرامپت تصویر: {p.Title}\n\n");
        sb.AppendLine($"موضوع: {p.Subject}");
        sb.AppendLine($"عناصر بصری: {p.VisualElements}");
        sb.AppendLine($"سبک: {p.Style}");
        sb.AppendLine($"ترکیب‌بندی: {p.Composition}");
        sb.AppendLine($"هدف آموزشی: {p.EducationalPurpose}");
        sb.AppendLine($"محدودیت متن: {p.TextRestrictions}");
        sb.AppendLine($"\nپرامپت نهایی (انگلیسی):\n{p.FinalPrompt}");
        return sb.ToString();
    }

    public static string VideoPrompt(VideoPromptDto v)
    {
        var sb = new StringBuilder($"پرامپت ویدئو: {v.Title}\n\n");
        sb.AppendLine($"توصیف صحنه: {v.SceneDescription}");
        sb.AppendLine($"شخصیت‌ها/اشیاء: {v.Characters}");
        sb.AppendLine($"محیط: {v.Environment}");
        sb.AppendLine($"دوربین: {v.Camera}");
        sb.AppendLine($"حرکت: {v.Motion}");
        sb.AppendLine($"مدت: {v.Duration}");
        sb.AppendLine($"هدف آموزشی: {v.EducationalObjective}");
        sb.AppendLine($"سبک بصری: {v.VisualStyle}");
        sb.AppendLine($"\nپرامپت نهایی (انگلیسی):\n{v.FinalPrompt}");
        return sb.ToString();
    }
}

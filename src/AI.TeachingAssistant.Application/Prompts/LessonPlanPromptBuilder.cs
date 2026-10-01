using System.Text;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Domain.Entities;

namespace AI.TeachingAssistant.Application.Prompts;

/// <summary>Builds the prompt for the lesson plan section (§9).</summary>
public static class LessonPlanPromptBuilder
{
    /// <summary>Specialist identity of the model for this section (layer 2, on top of the shared role).</summary>
    public static string System => "تو در این کار معلم باتجربه و طراح برنامه‌ی آموزشی (طراح طرح درس) هستی و طرح درس عملیاتی برای کلاس واقعی می‌نویسی.";

    /// <summary>Moderate creativity (§17) — structured enough, but not rigid.</summary>
    public const double Temperature = 0.7;

    public static string BuildUserPrompt(GenerationContext ctx)
    {
        var l = ctx.Lesson;
        var sb = new StringBuilder(PromptContextBuilder.Build(ctx));
        sb.AppendLine();
        sb.AppendLine("## خواسته: طرح درس");
        sb.AppendLine("یک طرح درس کامل برای کلاس بالا طراحی کن و فقط JSON با این ساختار برگردان:");
        sb.AppendLine("{");
        sb.AppendLine("  \"learningObjectives\": [\"...\"],");
        sb.AppendLine("  \"requiredMaterials\": [\"...\"],");
        sb.AppendLine("  \"segments\": [ { \"startMinute\": 0, \"endMinute\": 5, \"title\": \"...\", \"description\": \"...\" } ],");
        sb.AppendLine("  \"homework\": \"...\"");
        sb.AppendLine("}");
        sb.AppendLine("قواعد مهم:");
        sb.AppendLine("- بخش segments باید دقیقاً از دقیقه ۰ شروع شود و دقیقاً با مدت کلاس تمام شود؛ بدون فاصله یا هم‌پوشانی.");
        sb.AppendLine("- ۵ تا ۷ بخش منطقی با عنوان‌هایی مانند گرم‌کردن، معرفی، مفهوم اصلی، فعالیت، ارزشیابی، جمع‌بندی.");
        sb.AppendLine("- در description هر بخش مشخص کن: فعالیت معلم چه است، دانش‌آموز چه می‌کند و نتیجه‌ی آن بخش چه باید باشد؛ عبارت کلی مثل «موضوع توضیح داده می‌شود» کافی نیست.");
        sb.AppendLine("- objectives بین ۲ تا ۴ مورد، materials بین ۲ تا ۶ مورد باشد و فقط وسایلی را فهرست کن که در کلاس عادی در دسترس‌اند.");
        return sb.ToString();
    }
}

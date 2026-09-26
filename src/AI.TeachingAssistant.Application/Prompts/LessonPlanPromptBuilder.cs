using System.Text;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Domain.Entities;

namespace AI.TeachingAssistant.Application.Prompts;

/// <summary>Builds the prompt for the lesson plan section.</summary>
public static class LessonPlanPromptBuilder
{
    public static string System => "تو یک معلم باتجربه و طراح برنامه آموزشی هستی. فقط JSON معتبر برگردان؛ هیچ متن اضافه‌ای قبل یا بعد از JSON ننویس.";

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
        sb.AppendLine("- objectives بین ۲ تا ۴ مورد، materials بین ۲ تا ۶ مورد باشد.");
        sb.AppendLine("- محتوا برای پایه و سطح مشخص‌شده مناسب باشد.");
        return sb.ToString();
    }
}

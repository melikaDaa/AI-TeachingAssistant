using System.Text;
using AI.TeachingAssistant.Application.Common;

namespace AI.TeachingAssistant.Application.Prompts;

/// <summary>Builds the prompt for the educational content section.</summary>
public static class EducationalContentPromptBuilder
{
    public static string System => "تو یک نویسنده محتوای آموزشی برای کتاب‌های درسی هستی. فقط JSON معتبر برگردان؛ هیچ متن اضافه‌ای قبل یا بعد از JSON ننویس.";

    public static string BuildUserPrompt(GenerationContext ctx)
    {
        var sb = new StringBuilder(PromptContextBuilder.Build(ctx));
        sb.AppendLine();
        sb.AppendLine("## خواسته: محتوای آموزشی");
        sb.AppendLine("محتوای آموزشیِ آماده‌ی تدریس برای موضوع بالا بنویس و فقط JSON با این ساختار برگردان:");
        sb.AppendLine("{");
        sb.AppendLine("  \"simpleExplanation\": \"توضیح ساده و روان چند پاراگرافی\",");
        sb.AppendLine("  \"keyConcepts\": [\"...\"],");
        sb.AppendLine("  \"examples\": [\"...\"],");
        sb.AppendLine("  \"commonMisconceptions\": [\"...\"],");
        sb.AppendLine("  \"realWorldExample\": \"مثال کاربردی از زندگی روزمره\",");
        sb.AppendLine("  \"summary\": \"خلاصه کوتاه\"");
        sb.AppendLine("}");
        sb.AppendLine("قواعد مهم:");
        sb.AppendLine("- توضیح ساده بین ۱۵۰ تا ۳۰۰ کلمه؛ زبان متناسب با سطح دانش‌آموزان.");
        sb.AppendLine("- keyConcepts بین ۳ تا ۶ مورد، examples بین ۲ تا ۴ مورد، misconceptions بین ۲ تا ۳ مورد.");
        sb.AppendLine("- واژه‌های تخصصی را در اولین استفاده به زبان ساده توضیح بده.");
        return sb.ToString();
    }
}

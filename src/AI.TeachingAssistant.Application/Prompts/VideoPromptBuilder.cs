using System.Text;
using AI.TeachingAssistant.Application.Common;

namespace AI.TeachingAssistant.Application.Prompts;

/// <summary>Builds the prompt for the video prompt section.</summary>
public static class VideoPromptBuilder
{
    public static string System => "تو متخصص طراحی پرامپت برای ابزارهای تولید ویدئو با هوش مصنوعی هستی. فقط JSON معتبر برگردان؛ هیچ متن اضافه‌ای قبل یا بعد از JSON ننویس.";

    public static string BuildUserPrompt(GenerationContext ctx)
    {
        var sb = new StringBuilder(PromptContextBuilder.Build(ctx));
        sb.AppendLine();
        sb.AppendLine("## خواسته: پرامپت ویدئوی آموزشی");
        sb.AppendLine("دقیقاً ۱ پرامپت ویدئوی آموزشی برای موضوع بالا طراحی کن و فقط JSON با این ساختار برگردان:");
        sb.AppendLine("{");
        sb.AppendLine("  \"title\": \"عنوان کوتاه ویدئو\",");
        sb.AppendLine("  \"sceneDescription\": \"توصیف صحنه\",");
        sb.AppendLine("  \"characters\": \"شخصیت‌ها یا اشیاء اصلی\",");
        sb.AppendLine("  \"environment\": \"محیط و فضای ویدئو\",");
        sb.AppendLine("  \"camera\": \"حرکت دوربین\",");
        sb.AppendLine("  \"motion\": \"حرکت و تحرک در صحنه\",");
        sb.AppendLine("  \"duration\": \"مثلاً: ۳۰ ثانیه\",");
        sb.AppendLine("  \"educationalObjective\": \"هدف آموزشی ویدئو\",");
        sb.AppendLine("  \"visualStyle\": \"سبک بصری\",");
        sb.AppendLine("  \"finalPrompt\": \"پرامپت نهایی انگلیسی، آماده‌ی کپی در ابزار تولید ویدئو مانند Google Flow\"");
        sb.AppendLine("}");
        sb.AppendLine("قواعد مهم:");
        sb.AppendLine("- finalPrompt باید انگلیسی، توصیفی و قابل استفاده مستقیم در ابزارهای تولید ویدئو باشد.");
        sb.AppendLine("- ویدئو آموزشی، کوتاه و بدون دیالوگ پیچیده باشد.");
        sb.AppendLine("- هیچ متن یا زیرنویس داخل ویدئو خواسته نشود تا از حروف خراب جلوگیری شود.");
        return sb.ToString();
    }
}

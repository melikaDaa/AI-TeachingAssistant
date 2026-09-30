using System.Text;
using AI.TeachingAssistant.Application.Common;

namespace AI.TeachingAssistant.Application.Prompts;

/// <summary>Builds the prompt for the video prompt section.</summary>
public static class VideoPromptBuilder
{
    /// <summary>Specialist identity of the model for this section (layer 2, on top of the shared role).</summary>
    public static string System => "تو در این کار متخصص طراحی پرامپت برای ابزارهای تولید ویدئو با هوش مصنوعی هستی و صحنه‌های آموزشی کوتاه و قابل فهم طراحی می‌کنی.";

    /// <summary>Higher creativity (§17) — scene design benefits from imaginative angles.</summary>
    public const double Temperature = 0.8;

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
        sb.AppendLine("- ویدئو باید یکی از مفاهیم کلیدی همین درس را نشان دهد و با جریان کلاس هماهنگ باشد.");
        sb.AppendLine("- finalPrompt باید انگلیسیِ روان، توصیفی و قابل استفاده مستقیم در ابزارهای تولید ویدئو باشد؛ آن را با دقت املایی بنویس.");
        sb.AppendLine("- ویدئو آموزشی، کوتاه و بدون دیالوگ پیچیده باشد.");
        sb.AppendLine("- هیچ متن یا زیرنویس داخل ویدئو خواسته نشود تا از حروف خراب جلوگیری شود.");
        return sb.ToString();
    }
}

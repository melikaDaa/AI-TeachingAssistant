using System.Text;
using AI.TeachingAssistant.Application.Common;

namespace AI.TeachingAssistant.Application.Prompts;

/// <summary>Builds the prompt for the image prompt section (2 prompts).</summary>
public static class ImagePromptBuilder
{
    /// <summary>Specialist identity of the model for this section (layer 2, on top of the shared role).</summary>
    public static string System => "تو در این کار متخصص طراحی پرامپت برای ابزارهای تولید تصویر با هوش مصنوعی هستی و تصویرهای آموزشی دقیق برای مفاهیم درسی طراحی می‌کنی.";

    /// <summary>Higher creativity (§17) — visual concepts benefit from imaginative angles.</summary>
    public const double Temperature = 0.8;

    public static string BuildUserPrompt(GenerationContext ctx)
    {
        var sb = new StringBuilder(PromptContextBuilder.Build(ctx));
        sb.AppendLine();
        sb.AppendLine("## خواسته: پرامپت تصویر آموزشی");
        sb.AppendLine("دقیقاً ۲ پرامپت تصویرِ آموزشی برای موضوع بالا طراحی کن و فقط JSON با این ساختار برگردان:");
        sb.AppendLine("{");
        sb.AppendLine("  \"prompts\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"title\": \"عنوان کوتاه تصویر\",");
        sb.AppendLine("      \"subject\": \"موضوع تصویر\",");
        sb.AppendLine("      \"visualElements\": \"عناصر بصری موجود در تصویر\",");
        sb.AppendLine("      \"style\": \"سبک تصویر (مثلاً اینفوگرافیک آموزشی، تصویرسازی دیجیتال ملایم)\",");
        sb.AppendLine("      \"composition\": \"ترکیب‌بندی و زاویه دید\",");
        sb.AppendLine("      \"educationalPurpose\": \"این تصویر به فهم چه چیزی کمک می‌کند\",");
        sb.AppendLine("      \"textRestrictions\": \"محدودیت متن در تصویر\",");
        sb.AppendLine("      \"finalPrompt\": \"پرامپت نهایی انگلیسی، آماده‌ی کپی در ابزار تولید تصویر\"");
        sb.AppendLine("    }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("قواعد مهم:");
        sb.AppendLine("- دو تصویر باید دو هدف آموزشی متفاوت از هدف‌ها و مفاهیم کلیدی همین درس را پوشش دهند.");
        sb.AppendLine("- finalPrompt باید انگلیسیِ روان، مفصل و بهینه برای ابزارهایی مانند DALL-E یا Midjourney باشد؛ آن را با دقت املایی بنویس.");
        sb.AppendLine("- در finalPrompt صراحتاً بنویس: no text, no letters, no labels.");
        sb.AppendLine("- در textRestrictions بنویس که تصویر هیچ متن یا حروفی نداشته باشد تا از متن فارسی خراب جلوگیری شود.");
        return sb.ToString();
    }
}

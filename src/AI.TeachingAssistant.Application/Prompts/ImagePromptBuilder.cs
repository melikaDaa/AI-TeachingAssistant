using System.Text;
using AI.TeachingAssistant.Application.Common;

namespace AI.TeachingAssistant.Application.Prompts;

/// <summary>Builds the prompt for the image prompt section (2 prompts).</summary>
public static class ImagePromptBuilder
{
    public static string System => "تو متخصص طراحی پرامپت برای ابزارهای تولید تصویر با هوش مصنوعی هستی. فقط JSON معتبر برگردان؛ هیچ متن اضافه‌ای قبل یا بعد از JSON ننویس.";

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
        sb.AppendLine("- finalPrompt باید انگلیسی، مفصل و بهینه برای ابزارهایی مانند DALL-E یا Midjourney باشد.");
        sb.AppendLine("- در textRestrictions بنویس که تصویر هیچ متن یا حروفی نداشته باشد تا از متن فارسی خراب جلوگیری شود.");
        sb.AppendLine("- دو تصویر باید دو هدف آموزشی متفاوت را پوشش دهند.");
        return sb.ToString();
    }
}

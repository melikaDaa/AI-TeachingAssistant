using System.Text;
using AI.TeachingAssistant.Application.Common;

namespace AI.TeachingAssistant.Application.Prompts;

/// <summary>Builds the prompt for the quiz section (§12).</summary>
public static class QuizPromptBuilder
{
    /// <summary>Specialist identity of the model for this section (layer 2, on top of the shared role).</summary>
    public static string System => "تو در این کار طراح آزمون و سنجش آموزشی هستی و سؤال‌هایی می‌سازی که یادگیری واقعی را بسنجد، نه فقط حفظیات.";

    /// <summary>Lower randomness (§17) — quiz is the most structure-sensitive output (exact counts + answer keys).</summary>
    public const double Temperature = 0.3;

    public static string BuildUserPrompt(GenerationContext ctx)
    {
        var sb = new StringBuilder(PromptContextBuilder.Build(ctx));
        sb.AppendLine();
        sb.AppendLine("## خواسته: آزمون");
        sb.AppendLine("یک آزمون کوتاه از موضوع بالا طراحی کن و فقط JSON با این ساختار برگردان:");
        sb.AppendLine("{");
        sb.AppendLine("  \"questions\": [");
        sb.AppendLine("    { \"type\": \"mcq\", \"text\": \"...\", \"options\": [\"الف) ...\", \"ب) ...\", \"ج) ...\", \"د) ...\"], \"correctAnswer\": \"متن دقیق گزینه صحیح\", \"explanation\": \"توضیح یک‌جمله‌ای اینکه چرا این گزینه درست است\" },");
        sb.AppendLine("    { \"type\": \"shortAnswer\", \"text\": \"...\", \"correctAnswer\": \"پاسخ کوتاه نمونه\", \"explanation\": \"نکته کلیدی پاسخ\" }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("قواعد مهم:");
        sb.AppendLine("- دقیقاً ۸ سؤال به‌ترتیب: اول ۵ سؤال چهارگزینه‌ای (type = \"mcq\")، سپس ۳ سؤال تشریحی کوتاه (type = \"shortAnswer\").");
        sb.AppendLine("- هر سؤال باید مستقیماً از هدف‌های یادگیری یا مفاهیم کلیدی این درس باشد؛ از سؤال درباره جزئیاتی که در هدف‌ها و مفاهیم نیامده پرهیز کن.");
        sb.AppendLine("- تنوع سنجش: حداکثر ۲ سؤال حافظه‌محورِ ساده؛ بقیه باید فهم، کاربرد در موقعیت تازه یا مقایسه/دلیل‌آوری را بسنجند (مثلاً «اگر...چه می‌شود؟»، «چرا...؟»، تشبیه موقعیت واقعی).");
        sb.AppendLine("- correctAnswer برای mcq باید دقیقاً با متن یکی از گزینه‌ها یکسان باشد (شامل حرف الف/ب/ج/د).");
        sb.AppendLine("- گزینه‌های انحرافی معقول و هم‌مفهوم با موضوع باشند، نه گزینه‌های آشکارا بی‌ربط.");
        sb.AppendLine("- برای هر سؤال یک explanation کوتاه بنویس که چرا پاسخ درست درست است.");
        return sb.ToString();
    }
}

using System.Text;
using AI.TeachingAssistant.Application.Common;

namespace AI.TeachingAssistant.Application.Prompts;

/// <summary>Builds the prompt for the quiz section.</summary>
public static class QuizPromptBuilder
{
    public static string System => "تو طراح آزمون و سنجش آموزشی هستی. فقط JSON معتبر برگردان؛ هیچ متن اضافه‌ای قبل یا بعد از JSON ننویس.";

    public static string BuildUserPrompt(GenerationContext ctx)
    {
        var sb = new StringBuilder(PromptContextBuilder.Build(ctx));
        sb.AppendLine();
        sb.AppendLine("## خواسته: آزمون");
        sb.AppendLine("یک آزمون کوتاه از موضوع بالا طراحی کن و فقط JSON با این ساختار برگردان:");
        sb.AppendLine("{");
        sb.AppendLine("  \"questions\": [");
        sb.AppendLine("    { \"type\": \"mcq\", \"text\": \"...\", \"options\": [\"الف) ...\", \"ب) ...\", \"ج) ...\", \"د) ...\"], \"correctAnswer\": \"متن دقیق گزینه صحیح\" },");
        sb.AppendLine("    { \"type\": \"shortAnswer\", \"text\": \"...\", \"correctAnswer\": \"پاسخ کوتاه نمونه\" }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("قواعد مهم:");
        sb.AppendLine("- دقیقاً ۵ سؤال چهارگزینه‌ای (type = \"mcq\") و ۳ سؤال تشریحی کوتاه (type = \"shortAnswer\").");
        sb.AppendLine("- اول ۵ سؤال گزینه‌ای، بعد ۳ سؤال کوتاه.");
        sb.AppendLine("- correctAnswer برای mcq باید دقیقاً با متن یکی از گزینه‌ها یکسان باشد.");
        sb.AppendLine("- سطح دشواری متناسب با سطح دانش‌آموزان و مرتبط با مفاهیم اصلی موضوع باشد.");
        sb.AppendLine("- گزینه‌های انحرافی معقول و هم‌طول باشند.");
        return sb.ToString();
    }
}

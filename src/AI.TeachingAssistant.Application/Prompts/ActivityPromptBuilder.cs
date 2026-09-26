using System.Text;
using AI.TeachingAssistant.Application.Common;

namespace AI.TeachingAssistant.Application.Prompts;

/// <summary>Builds the prompt for the classroom activity section.</summary>
public static class ActivityPromptBuilder
{
    public static string System => "تو طراح فعالیت‌های یادگیری فعال در کلاس درس هستی. فقط JSON معتبر برگردان؛ هیچ متن اضافه‌ای قبل یا بعد از JSON ننویس.";

    public static string BuildUserPrompt(GenerationContext ctx)
    {
        var sb = new StringBuilder(PromptContextBuilder.Build(ctx));
        sb.AppendLine();
        sb.AppendLine("## خواسته: فعالیت کلاسی");
        sb.AppendLine("یک فعالیت عملی و تعاملی برای کلاس بالا طراحی کن و فقط JSON با این ساختار برگردان:");
        sb.AppendLine("{");
        sb.AppendLine("  \"title\": \"...\",");
        sb.AppendLine("  \"objective\": \"...\",");
        sb.AppendLine("  \"materials\": [\"...\"],");
        sb.AppendLine("  \"studentCount\": \"مثلاً: گروه‌های ۴ نفره (مناسب کلاس ۳۰ نفره)\",");
        sb.AppendLine("  \"durationMinutes\": 15,");
        sb.AppendLine("  \"instructions\": [\"گام ۱ ...\", \"گام ۲ ...\"],");
        sb.AppendLine("  \"expectedResult\": \"...\"");
        sb.AppendLine("}");
        sb.AppendLine("قواعد مهم:");
        sb.AppendLine("- فعالیت باید تعاملی باشد نه روخوانی منفعلانه؛ ترجیحاً گروهی و همراه با حرکت یا گفت‌وگو.");
        sb.AppendLine("- durationMinutes نباید از مدت کلاس بیشتر باشد.");
        sb.AppendLine("- instructions بین ۴ تا ۷ گام روشن و قابل اجرا در کلاس واقعی.");
        sb.AppendLine("- materials ساده و در دسترس (کاغذ، مقوا، ماژیک و ...).");
        return sb.ToString();
    }
}

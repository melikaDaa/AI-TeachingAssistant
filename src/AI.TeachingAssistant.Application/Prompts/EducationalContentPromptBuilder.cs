using System.Text;
using AI.TeachingAssistant.Application.Common;

namespace AI.TeachingAssistant.Application.Prompts;

/// <summary>Builds the prompt for the educational content section (§10).</summary>
public static class EducationalContentPromptBuilder
{
    /// <summary>Specialist identity of the model for this section (layer 2, on top of the shared role).</summary>
    public static string System => "تو در این کار نویسنده‌ی محتوای آموزشی برای کتاب درسی و کلاس درس هستی و مفاهیم را برای آموزش شفاهی معلم می‌نویسی.";

    /// <summary>Moderate creativity (§17) — same as the plan; prose quality matters here.</summary>
    public const double Temperature = 0.7;

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
        sb.AppendLine("- simpleExplanation یک توضیح پیوسته برای تدریس در کلاس است: از پرسش یا موقعیت آشنا شروع کن، مفهوم را قدم‌به‌قدم بساز، و مثال و تشبیه ملموس به کار ببر؛ نه یک مقاله‌ی دانشنامه‌ای.");
        sb.AppendLine("- توضیح ساده بین ۱۵۰ تا ۳۰۰ کلمه؛ زبان متناسب با سطح دانش‌آموزان.");
        sb.AppendLine("- هر مفهوم فقط یک‌بار کامل توضیح داده شود؛ از مقدمه‌ی کلی و عبارت‌های انگیزشیِ بی‌محتوا (مانند «در دنیای امروز») پرهیز کن.");
        sb.AppendLine("- keyConcepts بین ۳ تا ۶ مورد؛ هر مفهوم را با یک توضیح یک‌جمله‌ای ساده بنویس (مثلاً «هسته: مرکز فرماندهی سلول»).");
        sb.AppendLine("- examples بین ۲ تا ۴ مورد، misconceptions بین ۲ تا ۳ مورد؛ هر تصور غلط را به‌صورت «اشتباه رایج: ... / واقعیت: ...» بنویس.");
        sb.AppendLine("- واژه‌های تخصصی را در اولین استفاده به زبان ساده توضیح بده.");
        return sb.ToString();
    }
}

using System.Text;
using AI.TeachingAssistant.Application.Common;

namespace AI.TeachingAssistant.Application.Prompts;

/// <summary>Builds the prompt for the classroom activity section (§11).</summary>
public static class ActivityPromptBuilder
{
    /// <summary>Specialist identity of the model for this section (layer 2, on top of the shared role).</summary>
    public static string System => "تو در این کار طراح فعالیت‌های یادگیری فعال در کلاس درس هستی و فعالیت‌هایی طراحی می‌کنی که معلم بتواند بدون آماده‌سازی پیچیده همان روز اجرا کند.";

    /// <summary>Moderate creativity (§17) — activity design benefits from fresh ideas.</summary>
    public const double Temperature = 0.8;

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
        sb.AppendLine("- فعالیت باید تعاملی باشد نه روخوانی منفعلانه؛ نوع تعامل را با هدف درس بسنجید: فردی، دوبه‌دو، گروهی، حل مسئله، بحث، پیش‌بینی، آزمایش کوچک، سناریو، بازی یا نقشه‌ی مفهومی.");
        sb.AppendLine("- در هر گام مشخص کن معلم چه می‌کند و دانش‌آموز چه می‌کند؛ گام‌ها باید در کلاس واقعی همین امروز قابل اجرا باشند.");
        sb.AppendLine("- خروجی دانش‌آموز را ملموس بنویس؛ نه «یاد می‌گیرند»، بلکه چه چیزی تولید یا نشان می‌دهند (مثلاً نقشه‌ی مفهومی، طبقه‌بندی کارت‌ها، پاسخ شفاهی).");
        sb.AppendLine("- فعالیت باید مستقیماً یکی از هدف‌های یادگیری درس را تمرین دهد؛ در objective همان هدف را صریح بنویس.");
        sb.AppendLine("- durationMinutes نباید از مدت کلاس بیشتر باشد و با جریان کلاس هم‌خوان باشد.");
        sb.AppendLine("- instructions بین ۴ تا ۷ گام روشن و قابل اجرا در کلاس واقعی.");
        sb.AppendLine("- materials ساده و در دسترس (کاغذ، مقوا، ماژیک و ...).");
        sb.AppendLine("- اگر تعداد دانش‌آموزان مشخص شده، اندازه گروه‌ها را با آن تنظیم کن.");
        sb.AppendLine("- اگر سبک جلسه مشخص شده، نوع فعالیت را با آن هماهنگ کن (مشارکتی، توضیح‌محور، بازی‌محور، ترکیبی).");
        sb.AppendLine("- اگر امکانات کلاس مشخص شده، فقط از آن امکانات استفاده کن.");
        return sb.ToString();
    }
}

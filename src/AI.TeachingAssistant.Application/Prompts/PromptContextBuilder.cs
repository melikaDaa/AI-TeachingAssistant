using System.Text;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Domain.Entities;

namespace AI.TeachingAssistant.Application.Prompts;

/// <summary>
/// Layered Persian prompt context shared by every section builder (§4):
/// role → educational context → teacher context → lesson context →
/// (optional) shared context from earlier sections → global quality rules.
/// Section builders append only their own request + schema on top of this,
/// so instructions are never duplicated across builders (§36).
/// </summary>
public static class PromptContextBuilder
{
    /// <summary>
    /// Layer 1 — role. Written once here; per-section builders keep only their
    /// specialist angle, so the JSON-only instruction lives in exactly one place.
    /// </summary>
    public static string Role => """
        تو «مدرس‌یار» هستی؛ دستیار تولید محتوای آموزشی برای معلمان مدارس ایران.
        مخاطب نهایی خروجیِ تو یک معلم واقعی است که همین امروز می‌خواهد آن را در کلاس استفاده کند؛
        پس محتوایی عملی، مشخص و قابل اجرا بنویس، نه متن عمومی و کلی درباره موضوع.
        تنها خروجی تو JSON معتبر است: هیچ متن توضیحی، احوالپرسی، عذرخواهی یا بلاک مارک‌داون (```json) قبل یا بعد از JSON ننویس.
        """;

    /// <summary>
    /// Layer 5 — global quality rules applied to every section:
    /// natural Persian, no hallucinated facts, no invented numbers/references (§13/§14).
    /// </summary>
    public const string QualityRules = """
        قواعد کیفیت (در تمام بخش‌ها الزامی است):
        - فارسی روان و طبیعیِ مدرسه‌ای بنویس؛ از ترجمه‌ی تحت‌اللفظی، جمله‌های سنگین اداری و تکرار یک عبارت در چند بخش پرهیز کن.
        - تمام مقادیر JSON به جز finalPrompt باید فارسی باشد؛ هیچ واژه‌ای از زبان‌های دیگر (چینی، روسی، عربی، انگلیسی و ...) در متن فارسی ننویس.
        - اصطلاح علمی را فارسی بنویس؛ اگر معادل فارسی رایج ندارد، فارسی + معادل انگلیسی داخل پرانتز (مثلاً «میتوکندری (Mitochondria)»).
        - واقعیت علمی، عدد آماری، منبع، آزمایش یا کتاب درسیِ مشخصی از خودت نساز (هیچ hallucinationی). اگر مطمئن نیستی، محافظه‌کار و کلی بگو نه نادرست.
        - محتوا باید متناسب با پایه تحصیلی و سن دانش‌آموزان باشد و با سطح اعلام‌شده هم‌خوان بماند.
        - مثل گفتار یک معلم باتجربه بنویس، نه متن خشک و ماشینی؛ از عبارت‌های قالبی و تکراری پرهیز کن.
        """;

    public static string Build(GenerationContext ctx)
    {
        var l = ctx.Lesson;
        var t = ctx.Teacher;
        var sb = new StringBuilder();

        sb.AppendLine(Role);
        sb.AppendLine();
        sb.AppendLine("## مشخصات معلم");
        sb.AppendLine($"- نام: {t.FullName}");
        sb.AppendLine($"- مقطع: {t.Level}");
        sb.AppendLine($"- پایه: {t.Grade}");
        sb.AppendLine($"- دروس: {t.Subjects}");
        sb.AppendLine($"- سبک تدریس: {t.TeachingStyle}");
        sb.AppendLine();
        sb.AppendLine("## مشخصات درس");
        sb.AppendLine($"- عنوان: {l.Title}");
        sb.AppendLine($"- درس: {l.Subject}");
        sb.AppendLine($"- پایه: {l.Grade}");
        sb.AppendLine($"- موضوع: {l.Topic}");
        sb.AppendLine($"- مدت کلاس: {l.DurationMinutes} دقیقه");
        sb.AppendLine($"- سطح دانش‌آموزان: {l.StudentLevel}");
        if (!string.IsNullOrWhiteSpace(l.AdditionalInstructions))
            sb.AppendLine($"- توضیحات تکمیلی معلم: {l.AdditionalInstructions}");
        sb.AppendLine();
        sb.AppendLine(GenerationContext.LevelHint(l.StudentLevel));
        sb.AppendLine($"سبک تدریس معلم «{t.TeachingStyle}» است؛ خروجی را با این سبک هماهنگ کن.");

        AppendSharedContext(sb, ctx.Shared);
        AppendRegenerationBlock(sb, ctx.Regeneration);

        sb.AppendLine();
        sb.AppendLine(QualityRules);
        return sb.ToString();
    }

    /// <summary>
    /// Layer 6 — regeneration difference-reference (PART 2–4). The previous version is
    /// shown only to steer the model AWAY from it: never copy, paraphrase, or reuse its
    /// approach/structure/examples/wording. One shared Persian block for all six builders.
    /// </summary>
    private static void AppendRegenerationBlock(StringBuilder sb, RegenerationInfo? regen)
    {
        if (regen is null) return;

        sb.AppendLine();
        sb.AppendLine("## بازتولید این بخش");
        sb.AppendLine("این بخش قبلاً یک بار تولید شده و معلم «تولید مجدد» را زده است؛ یعنی یک جایگزینِ واقعاً متفاوت می‌خواهد، نه تکرار یا پارافریز نسخه‌ی قبلی.");
        sb.AppendLine("نسخه‌ی قبلی فقط برای اینکه تکراری تولید نکنی داده شده است؛ از آن به‌عنوان مرجعِ «پرهیز» استفاده کن:");
        sb.AppendLine(regen.PreviousDigest);
        sb.AppendLine("الزامات تغییر:");
        sb.AppendLine("- ساختار، رویکرد آموزشی، مثال‌ها و واژه‌های نسخه‌ی قبلی را بازنویسی یا پارافریز نکن؛ جایگزینی کاملاً متفاوت بنویس.");
        sb.AppendLine("- رویکرد آموزشی را عوض کن؛ برای فعالیت: فردی/دوبه‌دو/گروهی/حل مسئله/بحث/پیش‌بینی/آزمایش کوچک/سناریو/بازی/نقشه‌ی مفهومی — هر کدام با موضوع و هدف درس سازگارتر است.");
        sb.AppendLine("- هدف‌های یادگیری، مفاهیم کلیدی، موضوع، پایه و ساختار JSON خروجی باید دقیقاً حفظ شود.");
        sb.AppendLine("- محتوای جدید باید مستقل و قابل استفاده در کلاس واقعی باشد و همین قواعد کیفیت را رعایت کند.");
    }

    /// <summary>
    /// Layer 4 — the distilled context from earlier sections of the same package
    /// (§7/§8). Injected only when it exists, so the lesson plan is never locked to
    /// itself and per-section regenerate stays independent.
    /// </summary>
    private static void AppendSharedContext(StringBuilder sb, SharedLessonContext? shared)
    {
        if (shared is null) return;

        sb.AppendLine();
        sb.AppendLine("## هماهنگی با سایر بخش‌های این درس (از قبل تولید شده)");
        if (shared.Objectives.Count > 0)
        {
            sb.AppendLine("هدف‌های یادگیری این درس:");
            foreach (var o in shared.Objectives)
                sb.AppendLine($"- {o}");
        }
        if (!string.IsNullOrWhiteSpace(shared.LessonFlow))
            sb.AppendLine($"جریان کلاس: {shared.LessonFlow}");
        if (shared.KeyConcepts is { Count: > 0 })
        {
            sb.AppendLine("مفاهیم کلیدی محتوای آموزشی:");
            foreach (var k in shared.KeyConcepts)
                sb.AppendLine($"- {k}");
        }
        sb.AppendLine("خروجی تو باید دقیقاً با همین هدف‌ها، جریان کلاس و مفاهیم هماهنگ باشد؛ مفهوم جدیدی معرفی نکن که در آن‌ها نیامده و از همان واژه‌ها استفاده کن.");
    }
}

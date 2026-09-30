namespace AI.TeachingAssistant.Web.Extensions;

/// <summary>Metadata for the six package sections (keys match the REST API routes).</summary>
public static class SectionMeta
{
    public sealed record Info(int Code, string Key, string Emoji, string Title, string ProgressMessage);

    public static readonly IReadOnlyList<Info> All = new[]
    {
        new Info(1, "lesson-plan", "📚", "طرح درس", "در حال طراحی طرح درس..."),
        new Info(2, "content", "📝", "محتوای آموزشی", "در حال آماده‌سازی محتوای آموزشی..."),
        new Info(3, "activity", "🎯", "فعالیت کلاسی", "در حال ساخت فعالیت کلاسی..."),
        new Info(4, "quiz", "❓", "آزمون", "در حال طراحی آزمون..."),
        new Info(5, "image-prompt", "🖼", "پرامپت تصویر", "در حال آماده‌سازی پرامپت‌های تصویری..."),
        new Info(6, "video-prompt", "🎬", "پرامپت ویدئو", "در حال آماده‌سازی پرامپت ویدئویی..."),
    };

    public static Info? ByKey(string key) => All.FirstOrDefault(s => s.Key == key);
    public static Info ByCode(int code) => All.First(s => s.Code == code);

    /// <summary>Maps each section to the shared SVG icon family (no emoji).</summary>
    public static IconName IconOf(int code) => code switch
    {
        1 => IconName.Lesson,
        2 => IconName.Content,
        3 => IconName.Activity,
        4 => IconName.Quiz,
        5 => IconName.Image,
        6 => IconName.Video,
        _ => IconName.Content,
    };
}

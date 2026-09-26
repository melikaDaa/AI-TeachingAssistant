namespace AI.TeachingAssistant.Domain.Entities;

/// <summary>The six content sections of a teaching package (numeric codes are stable API contract).</summary>
public static class ContentTypes
{
    public const int LessonPlan = 1;
    public const int EducationalContent = 2;
    public const int Activity = 3;
    public const int Quiz = 4;
    public const int ImagePrompt = 5;
    public const int VideoPrompt = 6;

    public const int All = 0;

    public static readonly IReadOnlyList<int> AllSections = new[]
    {
        LessonPlan, EducationalContent, Activity, Quiz, ImagePrompt, VideoPrompt,
    };

    public static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string>
    {
        [LessonPlan] = "طرح درس",
        [EducationalContent] = "محتوای آموزشی",
        [Activity] = "فعالیت کلاسی",
        [Quiz] = "آزمون",
        [ImagePrompt] = "پرامپت تصویر",
        [VideoPrompt] = "پرامپت ویدئو",
    };
}

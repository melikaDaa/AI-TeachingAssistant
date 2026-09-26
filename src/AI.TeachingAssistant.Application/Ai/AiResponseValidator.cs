using AI.TeachingAssistant.Domain.Models;

namespace AI.TeachingAssistant.Application.Ai;

/// <summary>Per-section validation of structured AI output. Returns a Persian error message or null.</summary>
public static class AiResponseValidator
{
    public static string? Validate(LessonPlanModel plan, int durationMinutes)
    {
        if (plan.LearningObjectives.Count < 1) return "طرح درس فاقد هدف‌های آموزشی است.";
        if (plan.RequiredMaterials.Count < 1) return "طرح درس فاقد وسایل مورد نیاز است.";
        if (!plan.HasRealisticSchedule(durationMinutes))
            return "برنامه زمانی طرح درس با مدت کلاس هم‌خوانی ندارد.";
        return null;
    }

    public static string? Validate(EducationalContentModel content)
    {
        if (string.IsNullOrWhiteSpace(content.SimpleExplanation)) return "محتوای آموزشی خالی است.";
        if (content.KeyConcepts.Count < 1) return "محتوای آموزشی فاقد مفاهیم کلیدی است.";
        return null;
    }

    public static string? Validate(ActivityModel activity, int lessonDurationMinutes)
    {
        if (string.IsNullOrWhiteSpace(activity.Title)) return "فعالیت کلاسی بدون عنوان است.";
        if (activity.Instructions.Count < 2) return "فعالیت کلاسی فاقد دستورالعمل گام‌به‌گام است.";
        if (activity.DurationMinutes <= 0 || activity.DurationMinutes > lessonDurationMinutes)
            return "مدت فعالیت کلاسی با مدت کلاس هم‌خوانی ندارد.";
        return null;
    }

    public static string? Validate(QuizModel quiz)
    {
        if (quiz.Questions.Count != 8) return "آزمون باید شامل ۸ سؤال باشد (۵ گزینه‌ای و ۳ کوتاه).";
        if (quiz.MultipleChoice.Count() != 5) return "آزمون باید دقیقاً ۵ سؤال چهارگزینه‌ای داشته باشد.";
        if (quiz.ShortAnswers.Count() != 3) return "آزمون باید دقیقاً ۳ سؤال کوتاه داشته باشد.";

        foreach (var q in quiz.MultipleChoice)
        {
            if (q.Options is null || q.Options.Count != 4)
                return "هر سؤال گزینه‌ای باید دقیقاً ۴ گزینه داشته باشد.";
            if (string.IsNullOrWhiteSpace(q.CorrectAnswer) || !q.Options.Contains(q.CorrectAnswer))
                return "پاسخ صحیح یکی از سؤال‌های گزینه‌ای با گزینه‌های موجود هم‌خوانی ندارد.";
        }

        foreach (var q in quiz.ShortAnswers)
        {
            if (string.IsNullOrWhiteSpace(q.CorrectAnswer))
                return "برای سؤال‌های کوتاه پاسخ نمونه لازم است.";
        }

        return null;
    }

    public static string? Validate(IReadOnlyList<ImagePromptModel> prompts)
    {
        if (prompts.Count != 2) return "باید دقیقاً ۲ پرامپت تصویر تولید شود.";
        foreach (var p in prompts)
        {
            if (string.IsNullOrWhiteSpace(p.FinalPrompt)) return "یکی از پرامپت‌های تصویر خالی است.";
        }
        return null;
    }

    public static string? Validate(VideoPromptModel prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt.FinalPrompt)) return "پرامپت ویدئو خالی است.";
        return null;
    }
}

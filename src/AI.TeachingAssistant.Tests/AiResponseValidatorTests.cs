using AI.TeachingAssistant.Domain.Models;
using AI.TeachingAssistant.Application.Ai;
using Xunit;

namespace AI.TeachingAssistant.Tests;

public class AiResponseValidatorTests
{
    private static LessonPlanModel ValidPlan(int duration = 60) => new()
    {
        LearningObjectives = { "هدف ۱" },
        RequiredMaterials = { "تخته" },
        Segments =
        {
            new LessonPlanSegment { StartMinute = 0, EndMinute = 20, Title = "بخش ۱" },
            new LessonPlanSegment { StartMinute = 20, EndMinute = 60, Title = "بخش ۲" },
        },
    };

    [Fact]
    public void LessonPlan_Accepts_Contiguous_Schedule()
    {
        Assert.Null(AiResponseValidator.Validate(ValidPlan(), 60));
    }

    [Fact]
    public void LessonPlan_Rejects_Gaps_And_Overlaps()
    {
        var plan = ValidPlan();
        plan.Segments[1].StartMinute = 25; // gap
        Assert.NotNull(AiResponseValidator.Validate(plan, 60));
    }

    [Fact]
    public void LessonPlan_Rejects_Wrong_Total_Duration()
    {
        Assert.NotNull(AiResponseValidator.Validate(ValidPlan(), 45));
    }

    [Fact]
    public void Quiz_Accepts_Exactly_5_Mcq_And_3_Short()
    {
        var quiz = new QuizModel();
        for (var i = 0; i < 5; i++)
        {
            quiz.Questions.Add(new QuestionModel
            {
                Type = QuestionType.Mcq,
                Options = new List<string> { "الف", "ب", "ج", "د" },
                CorrectAnswer = "الف",
            });
        }
        for (var i = 0; i < 3; i++)
        {
            quiz.Questions.Add(new QuestionModel { Type = QuestionType.ShortAnswer, CorrectAnswer = "پاسخ" });
        }
        Assert.Null(AiResponseValidator.Validate(quiz));
    }

    [Fact]
    public void Quiz_Rejects_Wrong_Counts()
    {
        var quiz = new QuizModel();
        Assert.NotNull(AiResponseValidator.Validate(quiz));
    }

    [Fact]
    public void Quiz_Rejects_Answer_Not_In_Options()
    {
        var quiz = new QuizModel();
        quiz.Questions.Add(new QuestionModel
        {
            Type = QuestionType.Mcq,
            Options = new List<string> { "الف", "ب", "ج", "د" },
            CorrectAnswer = "هیچ‌کدام از این‌ها",
        });
        Assert.NotNull(AiResponseValidator.Validate(quiz));
    }
}

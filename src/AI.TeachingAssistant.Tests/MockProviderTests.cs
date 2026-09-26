using AI.TeachingAssistant.Application.Ai;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Application.Prompts;
using AI.TeachingAssistant.Application.Services;
using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Domain.Models;
using AI.TeachingAssistant.Infrastructure.Ai;
using Xunit;

namespace AI.TeachingAssistant.Tests;

/// <summary>The mock provider must produce output that passes validation — this keeps the demo path green.</summary>
public class MockProviderTests
{
    private static (Teacher Teacher, Lesson Lesson) Context(int duration = 60)
    {
        var teacher = new Teacher
        {
            Id = 1,
            FullName = "مریم احمدی",
            Level = TeachingLevels.MiddleSchool,
            Grade = "هفتم",
            Subjects = "علوم",
            TeachingStyle = "تعاملی",
        };
        var lesson = new Lesson
        {
            Id = 10,
            TeacherId = 1,
            Title = "سلول چیست؟",
            Subject = "علوم",
            Grade = "هفتم",
            Topic = "سلول",
            DurationMinutes = duration,
            StudentLevel = "متوسط",
        };
        return (teacher, lesson);
    }

    private static async Task<string> RawFor(string marker, int duration = 60)
    {
        var (teacher, lesson) = Context(duration);
        var ctx = new GenerationContext(teacher, lesson, 0);
        var prompt = marker switch
        {
            "plan" => LessonPlanPromptBuilder.BuildUserPrompt(ctx),
            "content" => EducationalContentPromptBuilder.BuildUserPrompt(ctx),
            "activity" => ActivityPromptBuilder.BuildUserPrompt(ctx),
            "quiz" => QuizPromptBuilder.BuildUserPrompt(ctx),
            "image" => ImagePromptBuilder.BuildUserPrompt(ctx),
            _ => VideoPromptBuilder.BuildUserPrompt(ctx),
        };
        var provider = new MockAIProvider();
        var response = await provider.CompleteAsync(new AiRequest("sys", prompt));
        return response.Content;
    }

    [Fact]
    public async Task Mock_LessonPlan_Passes_Validation()
    {
        var raw = await RawFor("plan");
        var model = AiJson.Parse<AiLessonPlan>(raw);
        Assert.NotNull(model);
        var domain = new LessonPlanModel
        {
            LearningObjectives = model!.LearningObjectives,
            RequiredMaterials = model.RequiredMaterials,
            Homework = model.Homework,
            Segments = model.Segments.Select(s => new LessonPlanSegment
            {
                StartMinute = s.StartMinute,
                EndMinute = s.EndMinute,
                Title = s.Title,
                Description = s.Description,
            }).ToList(),
        };
        Assert.Null(AiResponseValidator.Validate(domain, 60));
    }

    [Fact]
    public async Task Mock_Quiz_Has_Exactly_5_Mcq_And_3_Short_With_Valid_Keys()
    {
        var raw = await RawFor("quiz");
        var model = AiJson.Parse<AiQuiz>(raw);
        Assert.NotNull(model);
        var domain = new QuizModel
        {
            Questions = model!.Questions.Select(q => new QuestionModel
            {
                Type = string.Equals(q.Type, "shortAnswer", StringComparison.OrdinalIgnoreCase)
                    ? QuestionType.ShortAnswer
                    : QuestionType.Mcq,
                Text = q.Text,
                Options = q.Options,
                CorrectAnswer = q.CorrectAnswer,
            }).ToList(),
        };
        Assert.Null(AiResponseValidator.Validate(domain));
    }

    [Fact]
    public async Task Mock_ImagePrompts_Are_Two_With_Final_Prompt()
    {
        var raw = await RawFor("image");
        var model = AiJson.Parse<AI.TeachingAssistant.Application.Ai.AiImagePrompts>(raw);
        Assert.NotNull(model);
        Assert.Equal(2, model!.Prompts.Count);
        Assert.All(model.Prompts, p => Assert.False(string.IsNullOrWhiteSpace(p.FinalPrompt)));
    }

    [Fact]
    public async Task Mock_VideoPrompt_Has_Final_Prompt()
    {
        var raw = await RawFor("video");
        var model = AiJson.Parse<AI.TeachingAssistant.Application.Ai.AiVideoPrompt>(raw);
        Assert.NotNull(model);
        Assert.False(string.IsNullOrWhiteSpace(model!.FinalPrompt));
    }
}

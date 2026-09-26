using AI.TeachingAssistant.Application.Ai;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Application.Prompts;
using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Domain.Models;

namespace AI.TeachingAssistant.Application.Services;

/// <summary>
/// Generates one package section at a time: prompt → AI → parse → validate (one retry) → payload.
/// Isolated per section so regenerating the quiz never touches other sections.
/// </summary>
public class SectionGenerationService
{
    private const int MaxAttempts = 2;

    private readonly IAIProvider _ai;

    public SectionGenerationService(IAIProvider ai) => _ai = ai;

    public async Task<Result<LessonPlanModel>> GenerateLessonPlanAsync(GenerationContext ctx, CancellationToken ct = default)
    {
        var result = await RunAsync<LessonPlanModel, AiLessonPlan>(
            LessonPlanPromptBuilder.System,
            LessonPlanPromptBuilder.BuildUserPrompt(ctx),
            ai => new LessonPlanModel
            {
                LearningObjectives = ai.LearningObjectives,
                RequiredMaterials = ai.RequiredMaterials,
                Segments = ai.Segments.Select(s => new LessonPlanSegment
                {
                    StartMinute = s.StartMinute,
                    EndMinute = s.EndMinute,
                    Title = s.Title,
                    Description = s.Description,
                }).ToList(),
                Homework = ai.Homework,
            },
            m => AiResponseValidator.Validate(m, ctx.Lesson.DurationMinutes),
            ct);
        return result;
    }

    public async Task<Result<EducationalContentModel>> GenerateEducationalContentAsync(GenerationContext ctx, CancellationToken ct = default)
    {
        var result = await RunAsync<EducationalContentModel, AiEducationalContent>(
            EducationalContentPromptBuilder.System,
            EducationalContentPromptBuilder.BuildUserPrompt(ctx),
            ai => new EducationalContentModel
            {
                SimpleExplanation = ai.SimpleExplanation,
                KeyConcepts = ai.KeyConcepts,
                Examples = ai.Examples,
                CommonMisconceptions = ai.CommonMisconceptions,
                RealWorldExample = ai.RealWorldExample,
                Summary = ai.Summary,
            },
            AiResponseValidator.Validate,
            ct);
        return result;
    }

    public async Task<Result<ActivityModel>> GenerateActivityAsync(GenerationContext ctx, CancellationToken ct = default)
    {
        var result = await RunAsync<ActivityModel, AiActivity>(
            ActivityPromptBuilder.System,
            ActivityPromptBuilder.BuildUserPrompt(ctx),
            ai => new ActivityModel
            {
                Title = ai.Title,
                Objective = ai.Objective,
                Materials = ai.Materials,
                StudentCount = ai.StudentCount,
                DurationMinutes = ai.DurationMinutes,
                Instructions = ai.Instructions,
                ExpectedResult = ai.ExpectedResult,
            },
            m => AiResponseValidator.Validate(m, ctx.Lesson.DurationMinutes),
            ct);
        return result;
    }

    public async Task<Result<QuizModel>> GenerateQuizAsync(GenerationContext ctx, CancellationToken ct = default)
    {
        var result = await RunAsync<QuizModel, AiQuiz>(
            QuizPromptBuilder.System,
            QuizPromptBuilder.BuildUserPrompt(ctx),
            ai => new QuizModel
            {
                Questions = ai.Questions.Select(q => new QuestionModel
                {
                    Type = string.Equals(q.Type, "shortAnswer", StringComparison.OrdinalIgnoreCase) ? QuestionType.ShortAnswer : QuestionType.Mcq,
                    Text = q.Text,
                    Options = q.Options,
                    CorrectAnswer = q.CorrectAnswer,
                }).ToList(),
            },
            AiResponseValidator.Validate,
            ct);
        return result;
    }

    public async Task<Result<List<ImagePromptModel>>> GenerateImagePromptsAsync(GenerationContext ctx, CancellationToken ct = default)
    {
        var result = await RunAsync<List<ImagePromptModel>, AiImagePrompts>(
            ImagePromptBuilder.System,
            ImagePromptBuilder.BuildUserPrompt(ctx),
            ai => ai.Prompts.Select(p => new ImagePromptModel
            {
                Subject = p.Subject,
                VisualElements = p.VisualElements,
                Style = p.Style,
                Composition = p.Composition,
                EducationalPurpose = p.EducationalPurpose,
                TextRestrictions = p.TextRestrictions,
                FinalPrompt = p.FinalPrompt,
            }).ToList(),
            m => AiResponseValidator.Validate(m.AsReadOnly()),
            ct);
        return result;
    }

    public async Task<Result<VideoPromptModel>> GenerateVideoPromptAsync(GenerationContext ctx, CancellationToken ct = default)
    {
        var result = await RunAsync<VideoPromptModel, AiVideoPrompt>(
            VideoPromptBuilder.System,
            VideoPromptBuilder.BuildUserPrompt(ctx),
            ai => new VideoPromptModel
            {
                SceneDescription = ai.SceneDescription,
                Characters = ai.Characters,
                Environment = ai.Environment,
                Camera = ai.Camera,
                Motion = ai.Motion,
                Duration = ai.Duration,
                EducationalObjective = ai.EducationalObjective,
                VisualStyle = ai.VisualStyle,
                FinalPrompt = ai.FinalPrompt,
            },
            AiResponseValidator.Validate,
            ct);
        return result;
    }

    private async Task<Result<TDomain>> RunAsync<TDomain, TAi>(
        string systemPrompt,
        string userPrompt,
        Func<TAi, TDomain> map,
        Func<TDomain, string?> validate,
        CancellationToken ct)
    {
        string? lastError = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            AiResponse response;
            try
            {
                response = await _ai.CompleteAsync(new AiRequest(systemPrompt, userPrompt), ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Provider details stay in server logs (infrastructure layer); the teacher only sees a friendly message.
                return Result<TDomain>.Failure("در تولید محتوا مشکلی پیش آمد. لطفاً دوباره تلاش کنید.");
            }

            if (string.IsNullOrWhiteSpace(response.Content))
            {
                lastError = "پاسخی از هوش مصنوعی دریافت نشد.";
                continue;
            }

            var ai = AiJson.Parse<TAi>(response.Content);
            if (ai is null)
            {
                lastError = "پاسخ هوش مصنوعی قابل پردازش نبود.";
                continue;
            }

            var model = map(ai);
            var validationError = validate(model);
            if (validationError is not null)
            {
                lastError = validationError;
                continue;
            }

            return Result<TDomain>.Success(model);
        }

        return Result<TDomain>.Failure(lastError ?? "در تولید محتوا مشکلی پیش آمد.");
    }
}

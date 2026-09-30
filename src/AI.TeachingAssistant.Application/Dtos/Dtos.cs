using AI.TeachingAssistant.Domain.Entities;

namespace AI.TeachingAssistant.Application.Dtos;

// ---------- Teacher ----------
public sealed record TeacherDto(int Id, string FullName, string Level, string Grade, string Subjects, string TeachingStyle);

public sealed class UpdateTeacherRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public string Subjects { get; set; } = string.Empty;
    public string TeachingStyle { get; set; } = string.Empty;
}

// ---------- Lesson ----------
public sealed record LessonSummaryDto(
    int Id,
    string Title,
    string Subject,
    string Grade,
    string Topic,
    int DurationMinutes,
    string StudentLevel,
    PackageStatus Status,
    DateTime CreatedAt);

public sealed class CreateLessonRequest
{
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public int DurationMinutes { get; set; } = 60;
    public string StudentLevel { get; set; } = string.Empty;
    public string? AdditionalInstructions { get; set; }

    /// <summary>Numeric ContentTypes codes; empty/absent means the full package.</summary>
    public List<int>? RequestedSections { get; set; }
}

public sealed class UpdateLessonRequest
{
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public int DurationMinutes { get; set; } = 60;
    public string StudentLevel { get; set; } = string.Empty;
    public string? AdditionalInstructions { get; set; }
}

// ---------- Package sections ----------
public sealed record LessonPlanSegmentDto(int StartMinute, int EndMinute, string Title, string Description);

public sealed record LessonPlanDto(
    List<string> LearningObjectives,
    List<string> RequiredMaterials,
    List<LessonPlanSegmentDto> Segments,
    string Homework);

public sealed record EducationalContentDto(
    string SimpleExplanation,
    List<string> KeyConcepts,
    List<string> Examples,
    List<string> CommonMisconceptions,
    string RealWorldExample,
    string Summary);

public sealed record ActivityDto(
    string Title,
    string Objective,
    List<string> Materials,
    string StudentCount,
    int DurationMinutes,
    List<string> Instructions,
    string ExpectedResult);

public sealed record QuestionDto(string Type, string Text, List<string>? Options, string CorrectAnswer, string Explanation);

public sealed record QuizDto(int Id, List<QuestionDto> Questions);

public sealed record ImagePromptDto(
    int Id,
    string Title,
    string Subject,
    string VisualElements,
    string Style,
    string Composition,
    string EducationalPurpose,
    string TextRestrictions,
    string FinalPrompt);

public sealed record VideoPromptDto(
    int Id,
    string Title,
    string SceneDescription,
    string Characters,
    string Environment,
    string Camera,
    string Motion,
    string Duration,
    string EducationalObjective,
    string VisualStyle,
    string FinalPrompt);

public sealed record MediaPromptsDto(List<ImagePromptDto> ImagePrompts, VideoPromptDto? VideoPrompt);

// ---------- Package ----------
public sealed record GenerationProgressDto(string Status, string CurrentSection, List<string> CompletedSections, string? Error);

public sealed record TeachingPackageDto(
    int LessonId,
    string LessonTitle,
    GenerationProgressDto Progress,
    LessonPlanDto? LessonPlan,
    EducationalContentDto? EducationalContent,
    ActivityDto? Activity,
    QuizDto? Quiz,
    MediaPromptsDto Media,
    List<int> RequestedSections);

public sealed record SectionEditRequest(string PayloadJson);

namespace AI.TeachingAssistant.Application.Ai;

/// <summary>Structured shapes the AI is asked to return (camelCase JSON). Validated before persisting.</summary>
public sealed class AiLessonPlan
{
    public List<string> LearningObjectives { get; set; } = new();
    public List<string> RequiredMaterials { get; set; } = new();
    public List<AiSegment> Segments { get; set; } = new();
    public string Homework { get; set; } = string.Empty;
}

public sealed class AiSegment
{
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class AiEducationalContent
{
    public string SimpleExplanation { get; set; } = string.Empty;
    public List<string> KeyConcepts { get; set; } = new();
    public List<string> Examples { get; set; } = new();
    public List<string> CommonMisconceptions { get; set; } = new();
    public string RealWorldExample { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public sealed class AiActivity
{
    public string Title { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public List<string> Materials { get; set; } = new();
    public string StudentCount { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public List<string> Instructions { get; set; } = new();
    public string ExpectedResult { get; set; } = string.Empty;
}

public sealed class AiQuestion
{
    public string Type { get; set; } = "mcq";
    public string Text { get; set; } = string.Empty;
    public List<string>? Options { get; set; }
    public string CorrectAnswer { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}

public sealed class AiQuiz
{
    public List<AiQuestion> Questions { get; set; } = new();
}

public sealed class AiImagePrompt
{
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string VisualElements { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public string Composition { get; set; } = string.Empty;
    public string EducationalPurpose { get; set; } = string.Empty;
    public string TextRestrictions { get; set; } = string.Empty;
    public string FinalPrompt { get; set; } = string.Empty;
}

public sealed class AiImagePrompts
{
    public List<AiImagePrompt> Prompts { get; set; } = new();
}

public sealed class AiVideoPrompt
{
    public string Title { get; set; } = string.Empty;
    public string SceneDescription { get; set; } = string.Empty;
    public string Characters { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string Camera { get; set; } = string.Empty;
    public string Motion { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string EducationalObjective { get; set; } = string.Empty;
    public string VisualStyle { get; set; } = string.Empty;
    public string FinalPrompt { get; set; } = string.Empty;
}

namespace AI.TeachingAssistant.Domain.Models;

public enum QuestionType
{
    Mcq = 0,
    ShortAnswer = 1,
}

/// <summary>A single quiz question with its correct answer.</summary>
public sealed class QuestionModel
{
    public QuestionType Type { get; set; } = QuestionType.Mcq;

    public string Text { get; set; } = string.Empty;

    /// <summary>Options for multiple-choice questions (null for short-answer).</summary>
    public List<string>? Options { get; set; }

    /// <summary>Correct option text (mcq) or model answer (short answer).</summary>
    public string CorrectAnswer { get; set; } = string.Empty;

    /// <summary>Short teaching note explaining the answer (answer-key companion).</summary>
    public string Explanation { get; set; } = string.Empty;
}

/// <summary>Payload of the quiz section (stored as JSON).</summary>
public sealed class QuizModel
{
    public List<QuestionModel> Questions { get; set; } = new();

    public IEnumerable<QuestionModel> MultipleChoice => Questions.Where(q => q.Type == QuestionType.Mcq);
    public IEnumerable<QuestionModel> ShortAnswers => Questions.Where(q => q.Type == QuestionType.ShortAnswer);
}

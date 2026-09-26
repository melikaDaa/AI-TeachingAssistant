using AI.TeachingAssistant.Domain.Models;

namespace AI.TeachingAssistant.Domain.Entities;

/// <summary>Section D of the teaching package: the quiz with its answer key.</summary>
public class Quiz : BaseEntity
{
    public int TeachingPackageId { get; set; }
    public TeachingPackage TeachingPackage { get; set; } = null!;

    public string PayloadJson { get; set; } = string.Empty;

    public QuizModel? Model => PayloadJson.Deserialize<QuizModel>();
}

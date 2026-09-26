using AI.TeachingAssistant.Domain.Models;

namespace AI.TeachingAssistant.Domain.Entities;

/// <summary>Section A of the teaching package: the timed lesson plan.</summary>
public class LessonPlan : BaseEntity
{
    public int TeachingPackageId { get; set; }
    public TeachingPackage TeachingPackage { get; set; } = null!;

    public string PayloadJson { get; set; } = string.Empty;

    public LessonPlanModel? Model => PayloadJson.Deserialize<LessonPlanModel>();
}

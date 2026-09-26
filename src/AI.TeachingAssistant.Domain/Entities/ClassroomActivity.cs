using AI.TeachingAssistant.Domain.Models;

namespace AI.TeachingAssistant.Domain.Entities;

/// <summary>Section C of the teaching package: a practical classroom activity.</summary>
public class ClassroomActivity : BaseEntity
{
    public int TeachingPackageId { get; set; }
    public TeachingPackage TeachingPackage { get; set; } = null!;

    public string PayloadJson { get; set; } = string.Empty;

    public ActivityModel? Model => PayloadJson.Deserialize<ActivityModel>();
}

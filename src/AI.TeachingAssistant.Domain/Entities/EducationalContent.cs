using AI.TeachingAssistant.Domain.Models;

namespace AI.TeachingAssistant.Domain.Entities;

/// <summary>Section B of the teaching package: educational content for the topic.</summary>
public class EducationalContent : BaseEntity
{
    public int TeachingPackageId { get; set; }
    public TeachingPackage TeachingPackage { get; set; } = null!;

    public string PayloadJson { get; set; } = string.Empty;

    public EducationalContentModel? Model => PayloadJson.Deserialize<EducationalContentModel>();
}

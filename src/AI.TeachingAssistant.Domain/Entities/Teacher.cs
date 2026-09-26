namespace AI.TeachingAssistant.Domain.Entities;

/// <summary>The teacher using the assistant. MVP: a single demo teacher.</summary>
public class Teacher : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public string Subjects { get; set; } = string.Empty;
    public string TeachingStyle { get; set; } = string.Empty;
}

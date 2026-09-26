namespace AI.TeachingAssistant.Domain.Entities;

/// <summary>Base entity with common persistence fields.</summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

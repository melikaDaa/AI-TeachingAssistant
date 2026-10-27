namespace AI.TeachingAssistant.Application.Common;

public interface ICurrentTeacher
{
    int TeacherId { get; }
}

public sealed class TestCurrentTeacher : ICurrentTeacher
{
    public int TeacherId => 1;
}

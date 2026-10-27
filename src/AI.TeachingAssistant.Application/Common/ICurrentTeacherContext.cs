namespace AI.TeachingAssistant.Application.Common;

public interface ICurrentTeacherContext : ICurrentTeacher
{
    void Set(int teacherId);
}

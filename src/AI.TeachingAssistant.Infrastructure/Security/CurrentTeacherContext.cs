using AI.TeachingAssistant.Application.Common;

namespace AI.TeachingAssistant.Infrastructure.Security;

public sealed class CurrentTeacherContext : ICurrentTeacherContext
{
    public int TeacherId { get; private set; }

    public void Set(int teacherId)
    {
        if (teacherId <= 0) throw new ArgumentOutOfRangeException(nameof(teacherId));
        TeacherId = teacherId;
    }
}

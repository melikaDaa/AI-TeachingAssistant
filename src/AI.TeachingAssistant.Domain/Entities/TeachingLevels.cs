namespace AI.TeachingAssistant.Domain.Entities;

/// <summary>School level of the teacher (e.g. ابتدایی، متوسطه اول، متوسطه دوم).</summary>
public static class TeachingLevels
{
    public const string Primary = "ابتدایی";
    public const string MiddleSchool = "متوسطه اول";
    public const string HighSchool = "متوسطه دوم";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Primary, MiddleSchool, HighSchool,
    };
}

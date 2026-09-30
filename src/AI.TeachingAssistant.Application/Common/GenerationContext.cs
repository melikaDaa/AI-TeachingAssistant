using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Domain.Models;

namespace AI.TeachingAssistant.Application.Common;

/// <summary>
/// Structured context fed to every prompt builder:
/// teacher profile + lesson info + requested output + (optionally) the shared
/// context distilled from earlier sections of the same package (§7/§8).
/// </summary>
/// <param name="Shared">
/// Distilled objectives / lesson flow / key concepts from the lesson plan and content
/// sections already generated for this package. Null when nothing was generated yet or
/// when the section being generated is the lesson plan itself.
/// </param>
/// <param name="Regeneration">
/// Present only when THIS request regenerates an existing section: the compact
/// previous-output digest is a difference reference (PART 3), never a rewrite source.
/// </param>
public sealed record GenerationContext(
    Teacher Teacher,
    Lesson Lesson,
    int SectionType,
    SharedLessonContext? Shared = null,
    RegenerationInfo? Regeneration = null)
{
    public static string LevelHint(string studentLevel) => studentLevel switch
    {
        "مبتدی" => "دانش‌آموزان مبتدی هستند؛ زبان بسیار ساده و مثال‌های عینی به کار ببر.",
        "متوسط" => "دانش‌آموزان سطح متوسط دارند؛ توضیح روان با مثال‌های روزمره.",
        "پیشرفته" => "دانش‌آموزان پیشرفته هستند؛ محتوا را عمیق‌تر و چالشی‌تر بنویس.",
        _ => "سطح دانش‌آموزان را در نظر بگیر و زبان مناسب کلاس به کار ببر.",
    };
}

/// <summary>
/// Compact, token-efficient distillation of the sections already generated for the same
/// package (lesson plan → objectives + class flow; educational content → key concepts).
/// Only the essence is carried forward — never the full earlier output (§8).
/// </summary>
public sealed record SharedLessonContext(
    IReadOnlyList<string> Objectives,
    string LessonFlow,
    IReadOnlyList<string>? KeyConcepts = null)
{
    /// <summary>Builds the shared context from a freshly generated lesson plan.</summary>
    public static SharedLessonContext FromPlan(LessonPlanModel plan) => new(
        Objectives: plan.LearningObjectives.Take(4).ToList(),
        LessonFlow: string.Join(" → ", plan.Segments.Select(s => s.Title)));

    /// <summary>Enriches the shared context with key concepts once content is generated.</summary>
    public SharedLessonContext WithContent(EducationalContentModel content) => this with
    {
        KeyConcepts = content.KeyConcepts.Take(6).ToList(),
    };
}

/// <summary>
/// Marks a regeneration request and carries the previous version ONLY as a difference
/// reference (PART 3): the prompt tells the model to avoid its structure/wording, never
/// to rewrite it. The full previous output is never fed back verbatim.
/// </summary>
/// <param name="PreviousDigest">Compact digest of the previous output (titles/first sentences), not the whole text.</param>
/// <param name="PreviousText">Previous output reduced to distinct lines, for the duplicate safety check (PART 5).</param>
public sealed record RegenerationInfo(string PreviousDigest, string PreviousText);

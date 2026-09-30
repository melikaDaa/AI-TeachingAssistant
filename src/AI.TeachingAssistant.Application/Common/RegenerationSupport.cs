using System.Text.RegularExpressions;
using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Domain.Models;

namespace AI.TeachingAssistant.Application.Common;

/// <summary>
/// Regeneration support (PART 2–5): builds the compact difference-reference for the
/// previous output and detects near-duplicates with a lightweight word/line overlap.
/// No embeddings, no vector store — deterministic string math only (PART 15).
/// </summary>
public static class RegenerationSupport
{
    /// <summary>
    /// Normalizes the previous payload to distinct content lines (schema labels stripped),
    /// then reduces it to a small digest of openings plus a line list for the duplicate
    /// check. Returns null when there is nothing meaningful to compare.
    /// </summary>
    public static RegenerationInfo? Build(object? previousModel, int sectionType)
    {
        if (previousModel is null) return null;

        var lines = ExtractLines(previousModel, sectionType);
        if (lines.Count == 0) return null;

        return new RegenerationInfo(
            PreviousDigest: BuildDigest(lines),
            PreviousText: string.Join("\n", lines));
    }

    /// <summary>Extracts the previous output's meaningful lines from whichever section is being regenerated.</summary>
    private static List<string> ExtractLines(object model, int sectionType) => sectionType switch
    {
        ContentTypes.LessonPlan when model is LessonPlanModel p =>
            Texts(p.LearningObjectives, p.RequiredMaterials, p.Segments.Select(s => s.Title), p.Segments.Select(s => s.Description), new[] { p.Homework }),
        ContentTypes.EducationalContent when model is EducationalContentModel c =>
            Texts(new[] { c.SimpleExplanation, c.RealWorldExample, c.Summary }, c.KeyConcepts, c.Examples, c.CommonMisconceptions),
        ContentTypes.Activity when model is ActivityModel a =>
            Texts(new[] { a.Title, a.Objective, a.ExpectedResult }, a.Materials, a.Instructions),
        ContentTypes.Quiz when model is QuizModel q =>
            q.Questions.SelectMany(x => Texts(new[] { x.Text, x.CorrectAnswer, x.Explanation }, x.Options ?? [])).ToList(),
        ContentTypes.ImagePrompt when model is IReadOnlyList<ImagePromptModel> imgs =>
            imgs.SelectMany(p => Texts(new[] { p.Subject, p.VisualElements, p.Style, p.Composition, p.EducationalPurpose, p.TextRestrictions })).ToList(),
        ContentTypes.VideoPrompt when model is VideoPromptModel v =>
            Texts(new[] { v.SceneDescription, v.Characters, v.Environment, v.Camera, v.Motion, v.EducationalObjective, v.VisualStyle }),
        _ => [],
    };

    private static List<string> Texts(params IEnumerable<string?>[] groups) => groups
        .SelectMany(g => g)
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Select(s => s!.Trim())
        .Distinct(StringComparer.Ordinal)
        .ToList();

    /// <summary>Token-efficient digest: up to 8 short openings, enough to steer away, too little to copy.</summary>
    private static string BuildDigest(List<string> lines) => string.Join(
        "\n- ",
        lines.Where(l => l.Length > 8)
             .Select(l => l.Length <= 120 ? l : l[..120] + "…")
             .Take(8));

    /// <summary>
    /// True when a candidate is a near-duplicate of the previous output (PART 5).
    /// Fires only on BOTH heavy token overlap (≥0.65 Jaccard) AND line-level redundancy
    /// (≥60% of the previous lines fully reused), so normal shared vocabulary never
    /// rejects valid content.
    /// </summary>
    public static bool IsNearDuplicate(string candidateText, string previousText)
    {
        if (string.IsNullOrWhiteSpace(candidateText) || string.IsNullOrWhiteSpace(previousText))
            return false;

        var candidateTokens = Tokens(candidateText).ToHashSet();
        var previousTokens = Tokens(previousText).ToHashSet();
        if (candidateTokens.Count == 0 || previousTokens.Count == 0) return false;

        var overlap = candidateTokens.Intersect(previousTokens).Count();
        var union = candidateTokens.Union(previousTokens).Count();
        if (union == 0 || (double)overlap / union < 0.65) return false;

        // Line redundancy: a previous line counts as reused when its whole token set
        // (≥3 content words) reappears in the candidate.
        var previousLines = SplitLines(previousText)
            .Where(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 3)
            .ToList();
        if (previousLines.Count == 0) return false;

        var reused = previousLines.Count(pl => Tokens(pl).ToHashSet().IsSubsetOf(candidateTokens));
        return (double)reused / previousLines.Count >= 0.60;
    }

    private static IEnumerable<string> SplitLines(string text) => Regex
        .Split(text, @"[\n؛.!؟]+")
        .Select(l => l.Trim())
        .Where(l => l.Length > 0)
        .Distinct(StringComparer.Ordinal);

    /// <summary>Lower-cased word tokens ≥2 chars; parenthesized glossaries (فارسی (English)) are dropped (PART 5).</summary>
    private static IEnumerable<string> Tokens(string text)
    {
        var withoutGlossary = Regex.Replace(text, @"\([^)]*\)", " ");
        return Regex.Matches(withoutGlossary, @"[\p{L}\p{Nd}]+")
            .Select(m => m.Value.ToLowerInvariant())
            .Where(t => t.Length >= 2);
    }
}

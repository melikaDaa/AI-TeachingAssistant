using AI.TeachingAssistant.Application.Ai;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Application.Interfaces;
using AI.TeachingAssistant.Application.Prompts;
using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AI.TeachingAssistant.Application.Services;

/// <summary>
/// Generates one package section at a time: prompt → AI → parse → validate (one retry) → payload.
/// Isolated per section so regenerating the quiz never touches other sections.
/// </summary>
public class SectionGenerationService
{
    private const int MaxAttempts = 2;

    /// <summary>Appended once when a regeneration came back as a near-duplicate (PART 5).</summary>
    private const string DiversityHint = "\n\nتوجه ویژه: پاسخ قبلی‌ات تقریباً همان نسخه‌ی پیشین بود. این‌بار رویکرد را کاملاً عوض کن (مثلاً به‌جای کار گروهی، حل مسئله یا آزمایش یا بازی)؛ هیچ جمله‌ای را از نسخه‌ی پیشین تکرار نکن.";

    private readonly IAIProvider _ai;
    private readonly ILogger<SectionGenerationService>? _logger;

    public SectionGenerationService(IAIProvider ai, ILogger<SectionGenerationService>? logger = null)
    {
        _ai = ai;
        _logger = logger;
    }

    public async Task<Result<LessonPlanModel>> GenerateLessonPlanAsync(GenerationContext ctx, CancellationToken ct = default)
    {
        var result = await RunAsync<LessonPlanModel, AiLessonPlan>(
            LessonPlanPromptBuilder.System,
            LessonPlanPromptBuilder.BuildUserPrompt(ctx),
            LessonPlanPromptBuilder.Temperature,
            ai => new LessonPlanModel
            {
                LearningObjectives = ai.LearningObjectives,
                RequiredMaterials = ai.RequiredMaterials,
                Segments = ai.Segments.Select(s => new LessonPlanSegment
                {
                    StartMinute = s.StartMinute,
                    EndMinute = s.EndMinute,
                    Title = s.Title,
                    Description = s.Description,
                }).ToList(),
                Homework = ai.Homework,
            },
            m => AiResponseValidator.Validate(m, ctx.Lesson.DurationMinutes),
            ctx.Regeneration?.PreviousText,
            ct);
        return result;
    }

    public async Task<Result<EducationalContentModel>> GenerateEducationalContentAsync(GenerationContext ctx, CancellationToken ct = default)
    {
        var result = await RunAsync<EducationalContentModel, AiEducationalContent>(
            EducationalContentPromptBuilder.System,
            EducationalContentPromptBuilder.BuildUserPrompt(ctx),
            EducationalContentPromptBuilder.Temperature,
            ai => new EducationalContentModel
            {
                SimpleExplanation = ai.SimpleExplanation,
                KeyConcepts = ai.KeyConcepts,
                Examples = ai.Examples,
                CommonMisconceptions = ai.CommonMisconceptions,
                RealWorldExample = ai.RealWorldExample,
                Summary = ai.Summary,
            },
            AiResponseValidator.Validate,
            ctx.Regeneration?.PreviousText,
            ct);
        return result;
    }

    public async Task<Result<ActivityModel>> GenerateActivityAsync(GenerationContext ctx, CancellationToken ct = default)
    {
        var result = await RunAsync<ActivityModel, AiActivity>(
            ActivityPromptBuilder.System,
            ActivityPromptBuilder.BuildUserPrompt(ctx),
            ActivityPromptBuilder.Temperature,
            ai => new ActivityModel
            {
                Title = ai.Title,
                Objective = ai.Objective,
                Materials = ai.Materials,
                StudentCount = ai.StudentCount,
                DurationMinutes = ai.DurationMinutes,
                Instructions = ai.Instructions,
                ExpectedResult = ai.ExpectedResult,
            },
            m => AiResponseValidator.Validate(m, ctx.Lesson.DurationMinutes),
            ctx.Regeneration?.PreviousText,
            ct);
        return result;
    }

    public async Task<Result<QuizModel>> GenerateQuizAsync(GenerationContext ctx, CancellationToken ct = default)
    {
        var result = await RunAsync<QuizModel, AiQuiz>(
            QuizPromptBuilder.System,
            QuizPromptBuilder.BuildUserPrompt(ctx),
            QuizPromptBuilder.Temperature,
            ai => new QuizModel
            {
                Questions = ai.Questions.Select(q => new QuestionModel
                {
                    Type = string.Equals(q.Type, "shortAnswer", StringComparison.OrdinalIgnoreCase) ? QuestionType.ShortAnswer : QuestionType.Mcq,
                    Text = q.Text,
                    Options = q.Options,
                    CorrectAnswer = q.CorrectAnswer,
                    Explanation = q.Explanation,
                }).ToList(),
            },
            AiResponseValidator.Validate,
            ctx.Regeneration?.PreviousText,
            ct);
        return result;
    }

    public async Task<Result<List<ImagePromptModel>>> GenerateImagePromptsAsync(GenerationContext ctx, CancellationToken ct = default)
    {
        var result = await RunAsync<List<ImagePromptModel>, AiImagePrompts>(
            ImagePromptBuilder.System,
            ImagePromptBuilder.BuildUserPrompt(ctx),
            ImagePromptBuilder.Temperature,
            ai => ai.Prompts.Select(p => new ImagePromptModel
            {
                Subject = p.Subject,
                VisualElements = p.VisualElements,
                Style = p.Style,
                Composition = p.Composition,
                EducationalPurpose = p.EducationalPurpose,
                TextRestrictions = p.TextRestrictions,
                FinalPrompt = p.FinalPrompt,
            }).ToList(),
            m => AiResponseValidator.Validate(m.AsReadOnly()),
            ctx.Regeneration?.PreviousText,
            ct);
        return result;
    }

    public async Task<Result<VideoPromptModel>> GenerateVideoPromptAsync(GenerationContext ctx, CancellationToken ct = default)
    {
        var result = await RunAsync<VideoPromptModel, AiVideoPrompt>(
            VideoPromptBuilder.System,
            VideoPromptBuilder.BuildUserPrompt(ctx),
            VideoPromptBuilder.Temperature,
            ai => new VideoPromptModel
            {
                SceneDescription = ai.SceneDescription,
                Characters = ai.Characters,
                Environment = ai.Environment,
                Camera = ai.Camera,
                Motion = ai.Motion,
                Duration = ai.Duration,
                EducationalObjective = ai.EducationalObjective,
                VisualStyle = ai.VisualStyle,
                FinalPrompt = ai.FinalPrompt,
            },
            AiResponseValidator.Validate,
            ctx.Regeneration?.PreviousText,
            ct);
        return result;
    }

    private async Task<Result<TDomain>> RunAsync<TDomain, TAi>(
        string systemPrompt,
        string userPrompt,
        double temperature,
        Func<TAi, TDomain> map,
        Func<TDomain, string?> validate,
        string? previousText = null,
        CancellationToken ct = default)
    {
        string? lastError = null;
        var section = SectionName<TDomain>();
        var duplicateRetry = false;
        var promptTokens = 0;
        var completionTokens = 0;
        var totalTokens = 0;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            // Second attempt gets an explicit retry hint — real xKiro runs repeated the same
            // flawed answer verbatim (314 ms cached-style responses), so a plain re-ask is useless.
            // A near-duplicate regeneration additionally gets the stronger diversity hint (PART 5).
            var attemptPrompt = attempt == 1
                ? userPrompt
                : userPrompt + RetryHint + (duplicateRetry ? DiversityHint : "");

            AiResponse response;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                response = await _ai.CompleteAsync(new AiRequest(systemPrompt, attemptPrompt, Temperature: temperature), ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (AiProviderException ex) when (ex.IsTransient && attempt < MaxAttempts)
            {
                // Transient provider failures (timeout, network, 429, 5xx) get one more try;
                // the first xKiro E2E lost the whole plan section to a single 110 s hang.
                _logger?.LogWarning(ex, "Transient AI failure after {DurationMs} ms via {Provider} for {Section} — retrying", sw.ElapsedMilliseconds, _ai.Name, section);
                lastError = IsTeacherSafe(ex.Message) ? ex.Message : "در تولید محتوا مشکلی پیش آمد.";
                continue;
            }
            catch (AiProviderException ex)
            {
                // Only pass through provider messages that were written FOR the teacher
                // (Persian). English internals like "boom" must never reach the UI (§20).
                _logger?.LogWarning(ex, "AI generation failed after {DurationMs} ms via {Provider} for {Section}", sw.ElapsedMilliseconds, _ai.Name, section);
                return Result<TDomain>.Failure(IsTeacherSafe(ex.Message)
                    ? ex.Message
                    : "در تولید محتوا مشکلی پیش آمد. لطفاً دوباره تلاش کنید.");
            }
            catch (Exception)
            {
                // Provider details stay in server logs (infrastructure layer); the teacher only sees a friendly message.
                _logger?.LogWarning("AI generation failed after {DurationMs} ms via {Provider} for {Section}", sw.ElapsedMilliseconds, _ai.Name, section);
                return Result<TDomain>.Failure("در تولید محتوا مشکلی پیش آمد. لطفاً دوباره تلاش کنید.");
            }

            _logger?.LogInformation("AI call succeeded in {DurationMs} ms via {Provider} for {Section} (attempt {Attempt})", sw.ElapsedMilliseconds, _ai.Name, section, attempt);
            if (response.Usage is not null)
            {
                // Phase 2: accumulate real provider-reported usage across attempts.
                promptTokens += response.Usage.PromptTokens;
                completionTokens += response.Usage.CompletionTokens;
                totalTokens += response.Usage.TotalTokens;
            }

            if (string.IsNullOrWhiteSpace(response.Content))
            {
                lastError = "پاسخی از سرویس هوش مصنوعی دریافت نشد.";
                continue;
            }

            var ai = AiJson.Parse<TAi>(response.Content);
            if (ai is null)
            {
                lastError = "پاسخ سرویس هوش مصنوعی با ساختار مورد انتظار سازگار نبود.";
                continue;
            }

            var model = map(ai);
            var validationError = validate(model);
            if (validationError is not null)
            {
                lastError = validationError;
                continue;
            }

            var comparable = ToComparableText(model);

            // Cross-language corruption guard (§13): real runs leaked Chinese/Russian words
            // into Persian text. A hit costs one retry attempt, same as schema validation.
            var languageError = LanguageQualityCheck(model);
            if (languageError is not null)
            {
                // Phase 2 diagnostics: log WHAT leaked (short snippet only) — repeated guard
                // rejections are the main hidden token cost, and a generic message can't explain them.
                _logger?.LogWarning("Language guard rejected {Section} (attempt {Attempt}); offending snippet: {Snippet}", section, attempt, ForeignSnippet(comparable));
                lastError = languageError;
                continue;
            }

            // Near-duplicate guard for regenerations (PART 5): lightweight word/line overlap
            // against the previous output — no embeddings. Fires only on heavy overlap, so
            // normal shared vocabulary is never rejected. One bounded retry, then accept:
            // a usable near-duplicate beats an error for the teacher.
            if (previousText is not null && RegenerationSupport.IsNearDuplicate(comparable, previousText))
            {
                _logger?.LogWarning("Regenerated {Section} was a near-duplicate of the previous output (attempt {Attempt})", section, attempt);
                if (attempt < MaxAttempts)
                {
                    duplicateRetry = true;
                    lastError = "بازتولید بسیار شبیه نسخه‌ی قبلی بود؛ تلاش مجدد با تأکید بر تنوع انجام شد.";
                    continue;
                }
                _logger?.LogInformation("Accepting near-duplicate {Section} on final attempt to avoid failing the request", section);
            }

            LogSectionUsage(section, attempt, promptTokens, completionTokens, totalTokens);
            return Result<TDomain>.Success(model);
        }

        LogSectionUsage(section, MaxAttempts, promptTokens, completionTokens, totalTokens);
        return Result<TDomain>.Failure(lastError ?? "در تولید محتوا مشکلی پیش آمد.");
    }

    /// <summary>
    /// Flattens a model to Persian text for the duplicate check. English finalPrompt fields
    /// are excluded on purpose — they are generated separately and their overlap says
    /// nothing about whether the Persian content is a repeat.
    /// </summary>
    private static string ToComparableText(object? model) => model switch
    {
        LessonPlanModel p => string.Join('\n',
            p.LearningObjectives.Concat(p.RequiredMaterials)
                .Concat(p.Segments.Select(s => s.Title))
                .Concat(p.Segments.Select(s => s.Description))
                .Append(p.Homework)),
        EducationalContentModel c => string.Join('\n',
            new[] { c.SimpleExplanation, c.RealWorldExample, c.Summary }
                .Concat(c.KeyConcepts).Concat(c.Examples).Concat(c.CommonMisconceptions)),
        ActivityModel a => string.Join('\n',
            new[] { a.Title, a.Objective, a.ExpectedResult }.Concat(a.Materials).Concat(a.Instructions)),
        QuizModel q => string.Join('\n', q.Questions.SelectMany(x =>
            new[] { x.Text, x.CorrectAnswer, x.Explanation }.Concat(x.Options ?? []))),
        List<ImagePromptModel> imgs => string.Join('\n', imgs.SelectMany(p =>
            new[] { p.Subject, p.VisualElements, p.Style, p.Composition, p.EducationalPurpose, p.TextRestrictions })),
        VideoPromptModel v => string.Join('\n',
            new[] { v.SceneDescription, v.Characters, v.Environment, v.Camera, v.Motion, v.EducationalObjective, v.VisualStyle }),
        _ => string.Empty,
    };

    /// <summary>Friendly section name for logs (image prompts arrive as a generic List type).</summary>
    private static string SectionName<TDomain>() =>
        typeof(TDomain) == typeof(List<ImagePromptModel>) ? nameof(ImagePromptModel) : typeof(TDomain).Name;

    /// <summary>
    /// Short snippet (±40 chars) around the first offending character, for retry diagnostics.
    /// Never logs the whole generated text — just enough to understand a guard rejection.
    /// </summary>
    private static string ForeignSnippet(string text)
    {
        // Mirror the guard: parenthesized glossary and paper sizes are allowed, so they are
        // never reported as the offending snippet.
        text = System.Text.RegularExpressions.Regex.Replace(
            PaperSizePattern.Replace(System.Text.RegularExpressions.Regex.Replace(text, "\\([^)]*\\)", " "), " "),
            "\\s+", " ");
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '(') depth++;
            else if (c == ')') depth = Math.Max(0, depth - 1);
            else if (depth == 0 && (IsForeignChar(c) || char.IsAsciiLetter(c)))
            {
                var start = Math.Max(0, i - 40);
                var length = Math.Min(80, text.Length - start);
                return text.Substring(start, length).Replace('\n', ' ');
            }
        }
        return "(English finalPrompt or unlisted field)";
    }

    /// <summary>
    /// One lightweight per-section usage line (Phase 2). Sums real provider-reported tokens
    /// across attempts; silent for providers that report no usage (the offline mock), so no
    /// fabricated numbers ever appear in logs.
    /// </summary>
    private void LogSectionUsage(string section, int attempts, int promptTokens, int completionTokens, int totalTokens)
    {
        if (totalTokens <= 0) return;
        _logger?.LogInformation(
            "Token usage for {Section}: attempts={Attempts} promptTokens={Prompt} completionTokens={Completion} totalTokens={Total}",
            section, attempts, promptTokens, completionTokens, totalTokens);
    }

    /// <summary>Appended to the second attempt so the model sees what to fix (Persian-only; stays out of any output).</summary>
    private const string RetryHint = "\n\nتوجه: پاسخ قبلی شما قابل قبول نبود. این یک تلاش دوباره است؛ با دقت بیشتر قواعد بالا را رعایت کن.";

    private static bool IsTeacherSafe(string message) => message.Any(c => c is > (char)0x0600 and < (char)0x06FF);

    private static string? LanguageQualityCheck(object? model) => model switch
    {
        null => null,
        LessonPlanModel p => HasForeignScript(p.LearningObjectives)
                || HasForeignScript(p.RequiredMaterials)
                || HasForeignScript(p.Segments.Select(s => s.Title))
                || HasForeignScript(p.Segments.Select(s => s.Description))
                || HasForeignChars(p.Homework)
            ? "طرح درس حاوی واژه‌هایی به زبان‌های دیگر بود؛ تلاش مجدد انجام شد."
            : null,

        EducationalContentModel c => HasForeignScript(c.KeyConcepts)
                || HasForeignScript(c.Examples)
                || HasForeignScript(c.CommonMisconceptions)
                || HasForeignScript(new[] { c.SimpleExplanation, c.RealWorldExample, c.Summary })
            ? "محتوای آموزشی حاوی واژه‌هایی به زبان‌های دیگر بود؛ تلاش مجدد انجام شد."
            : null,

        ActivityModel a => HasForeignScript(a.Materials)
                || HasForeignScript(a.Instructions)
                || HasForeignScript(new[] { a.Title, a.Objective, a.StudentCount, a.ExpectedResult })
            ? "فعالیت کلاسی حاوی واژه‌هایی به زبان‌های دیگر بود؛ تلاش مجدد انجام شد."
            : null,

        QuizModel qz => qz.Questions.Any(q =>
                HasForeignChars(q.Text) ||
                HasForeignChars(q.CorrectAnswer) ||
                HasForeignChars(q.Explanation) ||
                (q.Options?.Any(HasForeignChars) ?? false))
            ? "آزمون حاوی واژه‌هایی به زبان‌های دیگر بود؛ تلاش مجدد انجام شد."
            : null,

        List<ImagePromptModel> imgs => imgs.Any(p =>
                HasForeignScript(new[] { p.Subject, p.VisualElements, p.Style, p.Composition, p.EducationalPurpose, p.TextRestrictions }) ||
                IsForeignScriptOnly(p.FinalPrompt))
            ? "پرامپت‌های تصویر حاوی واژه‌هایی به زبان‌های دیگر بودند؛ تلاش مجدد انجام شد."
            : null,

        VideoPromptModel v => HasForeignScript(new[] { v.SceneDescription, v.Characters, v.Environment, v.Camera, v.Motion, v.Duration, v.EducationalObjective, v.VisualStyle })
                || IsForeignScriptOnly(v.FinalPrompt)
            ? "پرامپت ویدئو حاوی واژه‌هایی به زبان‌های دیگر بود؛ تلاش مجدد انجام شد."
            : null,

        _ => null,
    };

    private static bool HasForeignScript(IEnumerable<string?> texts) => texts.Any(HasForeignChars);

    /// <summary>True when the text contains characters from non-Persian/English scripts (Chinese, Cyrillic, ...).</summary>
    private static bool HasForeignChars(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (text.Any(IsForeignChar)) return true;
        // Latin is allowed only as a parenthesized glossary term («میتوکندری (Mitochondria)», §13);
        // a stray Latin word inside Persian prose (e.g. "سلول‌های Tiny") fails the check.
        var withoutGlossary = System.Text.RegularExpressions.Regex.Replace(text, "\\([^)]*\\)", string.Empty);
        // Paper sizes («برگه A4») are standard classroom terms, not leaked prose: without this
        // whitelist the guard false-positived 10/10 activity attempts in one real session and
        // burned a retry (≈2,000 tokens) per attempt. Letter+digit tokens only — real word
        // leaks like "سلول‌های Tiny" still fail.
        withoutGlossary = PaperSizePattern.Replace(withoutGlossary, string.Empty);
        return withoutGlossary.Any(char.IsAsciiLetter);
    }

    private static readonly System.Text.RegularExpressions.Regex PaperSizePattern =
        new("\\b[Aa][1-9]\\b", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Foreign scripts only (no Latin rule) — for intentionally-English fields like finalPrompt.</summary>
    private static bool IsForeignScriptOnly(string? text) =>
        !string.IsNullOrEmpty(text) && text.Any(IsForeignChar);

    private static bool IsForeignChar(char c) =>
        // CJK
        (c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DBF) ||
        // Kana / Hangul
        (c >= 0x3040 && c <= 0x30FF) || (c >= 0xAC00 && c <= 0xD7AF) ||
        // Cyrillic
        (c >= 0x0400 && c <= 0x04FF) ||
        // Devanagari & Thai
        (c >= 0x0900 && c <= 0x097F) || (c >= 0x0E00 && c <= 0x0E7F);
}

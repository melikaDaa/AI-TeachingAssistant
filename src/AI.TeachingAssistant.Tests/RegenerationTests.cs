using AI.TeachingAssistant.Application.Ai;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Application.Prompts;
using AI.TeachingAssistant.Application.Services;
using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Domain.Models;
using AI.TeachingAssistant.Infrastructure.Ai;
using Xunit;

namespace AI.TeachingAssistant.Tests;

/// <summary>
/// Regeneration mechanics (PART 3/5): difference-reference digest, lightweight
/// near-duplicate detection, and the one bounded stronger-diversity retry.
/// </summary>
public class RegenerationTests
{
    private static GenerationContext Context(RegenerationInfo? regen = null) => new(
        new Teacher { FullName = "مریم", Level = "متوسطه اول", Grade = "هفتم", Subjects = "علوم", TeachingStyle = "تعاملی" },
        new Lesson { Title = "سلول چیست؟", Subject = "علوم", Grade = "هفتم", Topic = "سلول", DurationMinutes = 60, StudentLevel = "متوسط" },
        ContentTypes.Activity,
        Regeneration: regen);

    private static ActivityModel GroupCardSortActivity() => new()
    {
        Title = "فعالیت گروهی طبقه‌بندی اجزای سلول",
        Objective = "تمرین هدف یادگیری: شناسایی اجزای اصلی سلول",
        Materials = ["کارت‌های تصویری", "مقوا"],
        StudentCount = "گروه‌های ۴ نفره",
        DurationMinutes = 15,
        Instructions = ["معلم کارت‌ها را پخش می‌کند", "دانش‌آموزان کارت‌ها را طبقه‌بندی می‌کنند", "هر گروه نتیجه را ارائه می‌دهد"],
        ExpectedResult = "هر گروه اجزای سلول را درست طبقه‌بندی کند",
    };

    private static ActivityModel PredictionExperimentActivity() => new()
    {
        Title = "آزمایش کوچک پیش‌بینی و مشاهده",
        Objective = "تمرین هدف یادگیری: شناسایی اجزای اصلی سلول",
        Materials = ["ذره‌بین", "برگه گزارش"],
        StudentCount = "فعالیت فردی",
        DurationMinutes = 12,
        Instructions = [
            "هر دانش‌آموز پیش‌بینی می‌کند پوست پیاز زیر ذره‌بین چه شکلی است",
            "مشاهده می‌کند و آن را با پیش‌بینی مقایسه می‌کند",
            "تفاوت‌ها را در برگه گزارش می‌نویسد",
        ],
        ExpectedResult = "دانش‌آموز تفاوت پیش‌بینی و مشاهده را ثبت کند",
    };

    private static string Flatten(ActivityModel a) => string.Join('\n',
        new[] { a.Title, a.Objective, a.ExpectedResult }.Concat(a.Materials).Concat(a.Instructions));

    // ---------- digest / difference reference ----------

    [Fact]
    public void Build_From_Activity_Produces_Digest_And_Text()
    {
        var regen = RegenerationSupport.Build(GroupCardSortActivity(), ContentTypes.Activity);

        Assert.NotNull(regen);
        Assert.Contains("فعالیت گروهی", regen!.PreviousDigest);
        Assert.Contains("طبقه‌بندی", regen.PreviousText);
        // Digest is compact — far smaller than a full re-feed of the output.
        Assert.True(regen.PreviousDigest.Length < 800);
    }

    [Fact]
    public void Build_With_Null_Model_Returns_Null()
    {
        Assert.Null(RegenerationSupport.Build(null, ContentTypes.Activity));
    }

    // ---------- near-duplicate detection ----------

    [Fact]
    public void Verbatim_Repetition_Is_Detected_As_Duplicate()
    {
        var prev = RegenerationSupport.Build(GroupCardSortActivity(), ContentTypes.Activity)!;
        Assert.True(RegenerationSupport.IsNearDuplicate(prev.PreviousText, prev.PreviousText));
    }

    [Fact]
    public void Light_Paraphrase_With_Same_Structure_Is_Detected()
    {
        var prev = RegenerationSupport.Build(GroupCardSortActivity(), ContentTypes.Activity)!;
        var near = new ActivityModel
        {
            Title = "فعالیت گروهی دسته‌بندی اجزای سلول", // طبقه‌بندی → دسته‌بندی
            Objective = GroupCardSortActivity().Objective,
            Materials = GroupCardSortActivity().Materials,
            StudentCount = GroupCardSortActivity().StudentCount,
            DurationMinutes = 15,
            Instructions = ["معلم کارت‌ها را میان دانش‌آموزان پخش می‌کند", "دانش‌آموزان کارت‌ها را دسته‌بندی می‌کنند", "هر گروه نتیجه‌ی خود را ارائه می‌دهد"],
            ExpectedResult = GroupCardSortActivity().ExpectedResult,
        };

        Assert.True(RegenerationSupport.IsNearDuplicate(Flatten(near), prev.PreviousText));
    }

    [Fact]
    public void Genuinely_Different_Activity_Is_Not_Flagged()
    {
        var prev = RegenerationSupport.Build(GroupCardSortActivity(), ContentTypes.Activity)!;
        var other = PredictionExperimentActivity();

        // Same objective is normal and must not trip the check.
        Assert.False(RegenerationSupport.IsNearDuplicate(Flatten(other), prev.PreviousText));
    }

    [Fact]
    public void Shared_Vocabulary_Only_Is_Not_Flagged()
    {
        // Normal educational overlap: same topic words, completely different sentences.
        var prev = "دانش‌آموزان سلول را با استفاده از کارت‌های تصویری در گروه‌های چهارنفره طبقه‌بندی می‌کنند و اجزای سلول را نام می‌برند";
        var candidate = "سلول واحد ساختاری بدن است؛ معلم پرسش‌های هدایت‌شده می‌پرسد تا دانش‌آموزان ویژگی‌های زنده بودن سلول را استدلال کنند";
        Assert.False(RegenerationSupport.IsNearDuplicate(candidate, prev));
    }

    // ---------- duplicate-retry in the generation service ----------

    private sealed class ScriptedProvider(string first, string? second = null) : IAIProvider
    {
        private int _calls;
        public List<string> Prompts { get; } = [];
        public string Name => "scripted";
        public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken cancellationToken = default)
        {
            Prompts.Add(request.UserPrompt);
            var call = ++_calls;
            return Task.FromResult(new AiResponse(call == 1 ? first : (second ?? first)));
        }
    }

    private static string ActivityJson(ActivityModel a) => PayloadJson.Serialize(new AiActivity
    {
        Title = a.Title,
        Objective = a.Objective,
        Materials = a.Materials.ToList(),
        StudentCount = a.StudentCount,
        DurationMinutes = a.DurationMinutes,
        Instructions = a.Instructions.ToList(),
        ExpectedResult = a.ExpectedResult,
    });

    [Fact]
    public async Task Near_Duplicate_First_Attempt_Triggers_Diversity_Retry()
    {
        var prev = RegenerationSupport.Build(GroupCardSortActivity(), ContentTypes.Activity)!;
        var provider = new ScriptedProvider(
            ActivityJson(GroupCardSortActivity()),      // attempt 1: near-duplicate of previous
            ActivityJson(PredictionExperimentActivity())); // attempt 2: genuinely different
        var service = new SectionGenerationService(provider);

        var result = await service.GenerateActivityAsync(Context(prev));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(2, provider.Prompts.Count);
        // The retry prompt must carry the stronger diversity instruction.
        Assert.Contains("توجه ویژه", provider.Prompts[1]);
        Assert.DoesNotContain("توجه ویژه", provider.Prompts[0]);
    }

    [Fact]
    public async Task Different_First_Attempt_Sends_Single_Request()
    {
        var prev = RegenerationSupport.Build(GroupCardSortActivity(), ContentTypes.Activity)!;
        var provider = new ScriptedProvider(ActivityJson(PredictionExperimentActivity()));
        var service = new SectionGenerationService(provider);

        var result = await service.GenerateActivityAsync(Context(prev));

        Assert.True(result.Ok, result.Error);
        Assert.Single(provider.Prompts);
    }

    // ---------- regeneration prompt injection ----------

    [Fact]
    public void Regeneration_Prompt_Contains_Difference_Reference_Block()
    {
        var prev = RegenerationSupport.Build(GroupCardSortActivity(), ContentTypes.Activity)!;
        var prompt = ActivityPromptBuilder.BuildUserPrompt(Context(prev));

        Assert.Contains("بازتولید", prompt);
        Assert.Contains(prev.PreviousDigest, prompt);
        Assert.Contains("پارافریز نکن", prompt);
        // First generation must NOT contain the regeneration block.
        var normal = ActivityPromptBuilder.BuildUserPrompt(Context());
        Assert.DoesNotContain("بازتولید", normal);
    }

    [Fact]
    public void Regeneration_Block_Lists_Variation_Dimensions()
    {
        var prev = RegenerationSupport.Build(GroupCardSortActivity(), ContentTypes.Activity)!;
        var prompt = ActivityPromptBuilder.BuildUserPrompt(Context(prev));

        Assert.Contains("فردی", prompt);
        Assert.Contains("حل مسئله", prompt);
        Assert.Contains("نقشه‌ی مفهومی", prompt);
    }
}

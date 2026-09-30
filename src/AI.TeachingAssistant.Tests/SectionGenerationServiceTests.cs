using AI.TeachingAssistant.Application.Ai;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Application.Services;
using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Infrastructure.Ai;
using Xunit;

namespace AI.TeachingAssistant.Tests;

/// <summary>A provider that always fails, to prove errors turn into graceful Persian failures.</summary>
internal sealed class FailingProvider : IAIProvider
{
    public string Name => "failing";
    public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken cancellationToken = default)
        => throw new AiProviderException("boom");
}

/// <summary>A provider that fails with a safe Persian message (mirrors the real provider mapping).</summary>
internal sealed class PersianFailingProvider : IAIProvider
{
    public string Name => "persian-failing";
    public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken cancellationToken = default)
        => throw new AiProviderException("احراز هویت سرویس هوش مصنوعی ناموفق بود.");
}

/// <summary>A provider that returns garbage instead of JSON.</summary>
internal sealed class GarbageProvider : IAIProvider
{
    public string Name => "garbage";
    public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new AiResponse("متاسفانه نمی‌توانم پاسخ JSON بدهم"));
}

public class SectionGenerationServiceTests
{
    private static GenerationContext Context() => new(
        new Teacher { FullName = "مریم", Level = "متوسطه اول", Grade = "هفتم", Subjects = "علوم", TeachingStyle = "تعاملی" },
        new Lesson { Title = "سلول چیست؟", Subject = "علوم", Grade = "هفتم", Topic = "سلول", DurationMinutes = 60, StudentLevel = "متوسط" },
        ContentTypes.Quiz);

    [Fact]
    public async Task Provider_Failure_Becomes_Friendly_Persian_Error()
    {
        var service = new SectionGenerationService(new FailingProvider());
        var result = await service.GenerateQuizAsync(Context());
        Assert.False(result.Ok);
        Assert.Equal("در تولید محتوا مشکلی پیش آمد. لطفاً دوباره تلاش کنید.", result.Error);
    }

    [Fact]
    public async Task Safe_Provider_Message_Is_Passed_To_The_Teacher()
    {
        var service = new SectionGenerationService(new PersianFailingProvider());
        var result = await service.GenerateQuizAsync(Context());
        Assert.False(result.Ok);
        Assert.Equal("احراز هویت سرویس هوش مصنوعی ناموفق بود.", result.Error);
    }

    [Fact]
    public async Task Garbage_Response_Becomes_Friendly_Error_After_Retries()
    {
        var service = new SectionGenerationService(new GarbageProvider());
        var result = await service.GenerateQuizAsync(Context());
        Assert.False(result.Ok);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task Mock_Provider_Produces_Valid_Content_End_To_End()
    {
        var service = new SectionGenerationService(new MockAIProvider());
        var result = await service.GenerateQuizAsync(Context());
        Assert.True(result.Ok, result.Error);
        Assert.Equal(8, result.Value!.Questions.Count);
    }
}

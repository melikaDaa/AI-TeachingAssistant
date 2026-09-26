using System.Threading.Channels;
using AI.TeachingAssistant.Application.Interfaces;
using AI.TeachingAssistant.Application.Services;
using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AI.TeachingAssistant.Infrastructure.Generation;

/// <summary>In-memory FIFO of generation jobs (MVP single-instance).</summary>
public sealed class GenerationJobQueue : IGenerationJobQueue
{
    private readonly Channel<(int LessonId, int SectionType)> _channel =
        Channel.CreateUnbounded<(int LessonId, int SectionType)>();

    public void Enqueue(int lessonId, int sectionType) => _channel.Writer.TryWrite((lessonId, sectionType));

    public IAsyncEnumerable<(int LessonId, int SectionType)> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}

/// <summary>
/// Runs generation jobs one at a time in the background so HTTP requests return instantly.
/// The API keeps serving progress from the database while this works.
/// </summary>
public sealed class GenerationJobWorker : BackgroundService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly GenerationJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GenerationJobWorker> _logger;

    public GenerationJobWorker(GenerationJobQueue queue, IServiceScopeFactory scopeFactory, ILogger<GenerationJobWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (lessonId, sectionType) in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await Gate.WaitAsync(stoppingToken);
                using var scope = _scopeFactory.CreateScope();
                var coordinator = scope.ServiceProvider.GetRequiredService<GenerationCoordinator>();

                if (sectionType == ContentTypes.All)
                {
                    _logger.LogInformation("Job: full package for lesson {LessonId}", lessonId);
                    await coordinator.GeneratePackageAsync(lessonId, stoppingToken);
                }
                else
                {
                    _logger.LogInformation("Job: section {Section} for lesson {LessonId}", sectionType, lessonId);
                    await coordinator.GenerateSectionAsync(lessonId, sectionType, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Generation job failed for lesson {LessonId}", lessonId);
                await MarkFailedAsync(lessonId);
            }
            finally
            {
                Gate.Release();
            }
        }
    }

    private async Task MarkFailedAsync(int lessonId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var lessons = scope.ServiceProvider.GetRequiredService<ILessonRepository>();
            var lesson = await lessons.GetByIdAsync(lessonId, ct: CancellationToken.None);
            if (lesson is not null)
            {
                lesson.PackageStatus = PackageStatus.Failed;
                lesson.LastError = "در تولید محتوا مشکلی پیش آمد. لطفاً دوباره تلاش کنید.";
                await lessons.UpdateAsync(lesson, CancellationToken.None);
            }
        }
        catch (Exception inner)
        {
            _logger.LogError(inner, "Could not mark lesson {LessonId} as failed", lessonId);
        }
    }
}

using AI.TeachingAssistant.Application.Dtos;
using AI.TeachingAssistant.Application.Interfaces;
using AI.TeachingAssistant.Application.Mapping;
using AI.TeachingAssistant.Application.Services;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Infrastructure.Ai;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AI.TeachingAssistant.Api.Controllers;

/// <summary>Lessons REST API (spec §21). Controllers stay thin; logic lives in services.</summary>
[ApiController]
[Route("api/lessons")]
[Produces("application/json")]
public class LessonsController : ControllerBase
{
    private readonly LessonService _lessons;
    private readonly PackageSectionService _sections;
    private readonly IGenerationJobQueue _jobs;
    private readonly ILessonRepository _repository;
    private readonly IUsageService _usage;
    private readonly ImageGenerationOptions _imageOptions;

    public LessonsController(
        LessonService lessons,
        PackageSectionService sections,
        IGenerationJobQueue jobs,
        ILessonRepository repository,
        IUsageService usage,
        ImageGenerationOptions imageOptions)
    {
        _lessons = lessons;
        _sections = sections;
        _jobs = jobs;
        _repository = repository;
        _usage = usage;
        _imageOptions = imageOptions;
    }

    [HttpPost]
    public async Task<ActionResult<LessonSummaryDto>> Create([FromBody] CreateLessonRequest request, CancellationToken ct)
    {
        var created = await _lessons.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet]
    public async Task<ActionResult<List<LessonSummaryDto>>> List(CancellationToken ct)
        => Ok(await _lessons.ListAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TeachingPackageDto>> GetById(int id, CancellationToken ct)
    {
        var lesson = await _repository.GetForTeacherAsync(id, _lessons.CurrentTeacherId, includePackage: true, ct);
        if (lesson is null) return NotFound(new { error = "درس یافت نشد." });
        return Ok(lesson.ToPackageDto());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<LessonSummaryDto>> Update(int id, [FromBody] UpdateLessonRequest request, CancellationToken ct)
        => Ok(await _lessons.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _lessons.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Queues generation of the requested sections (or full package) and returns immediately.</summary>
    [EnableRateLimiting("ai")]
    [HttpPost("{id:int}/generate")]
    public async Task<IActionResult> Generate(int id, CancellationToken ct)
    {
        var lesson = await _repository.GetForTeacherAsync(id, _lessons.CurrentTeacherId, includePackage: false, ct);
        if (lesson is null) return NotFound(new { error = "درس یافت نشد." });
        var requested = lesson.RequestedSections.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var value) ? value : -1).Where(x => x > 0).Distinct().ToArray();
        if (requested.Length == 0) requested = ContentTypes.AllSections.ToArray();
        var operations = requested.Select(MapOperation).ToArray();
        if (operations.Contains("image-prompt") && _imageOptions.IsEnabled) operations = [.. operations, "image"];
        var key = Guid.NewGuid().ToString("N");
        if (!await _usage.AdmitAsync(_lessons.CurrentTeacherId, operations, key, ct, _imageOptions.IsEnabled ? 3 : 0))
        {
            Response.Headers.RetryAfter = "60";
            return StatusCode(429, new { error = "به سقف کوتاه‌مدت یا سهمیه امروز رسیده‌اید. کمی بعد یا پس از بازنشانی روزانه دوباره تلاش کنید." });
        }
        try { await _jobs.EnqueueAsync(id, _lessons.CurrentTeacherId, ContentTypes.All, key, ct); }
        catch { await _usage.ReleaseReservationsAsync(key, ct); throw; }
        return Accepted(new { status = "generating" });
    }

    /// <summary>Regenerates one section only; other sections are never touched (spec §8).</summary>
    [EnableRateLimiting("ai")]
    [HttpPost("{id:int}/generate/{section}")]
    public async Task<IActionResult> GenerateSection(int id, string section, CancellationToken ct)
    {
        var lesson = await _repository.GetForTeacherAsync(id, _lessons.CurrentTeacherId, includePackage: false, ct);
        if (lesson is null) return NotFound(new { error = "درس یافت نشد." });

        var sectionType = SectionCode(section);
        if (sectionType is null) return BadRequest(new { error = "نوع محتوای نامعتبر است." });
        var operation = MapOperation(sectionType.Value);
        var operations = sectionType == ContentTypes.ImagePrompt && _imageOptions.IsEnabled ? new[] { operation, "image" } : [operation];
        if (lesson.Package?.CompletedSections.Contains(sectionType.Value.ToString()) == true && operation != "image-prompt")
            operation = "regenerate";
        var key = Guid.NewGuid().ToString("N");
        if (!await _usage.AdmitAsync(_lessons.CurrentTeacherId, operations, key, ct, _imageOptions.IsEnabled ? 3 : 0))
        {
            Response.Headers.RetryAfter = "60";
            return StatusCode(429, new { error = "به سقف کوتاه‌مدت یا سهمیه امروز رسیده‌اید. کمی بعد یا پس از بازنشانی روزانه دوباره تلاش کنید." });
        }

        // Flip the status synchronously BEFORE the 202 (PART 18): the very first poll after
        // enqueue must see "generating", otherwise it can mistake the regenerate for an
        // instant no-op and show stale content. The worker flips it too, as a safety net.
        if (lesson.PackageStatus is not PackageStatus.Generating)
        {
            lesson.PackageStatus = PackageStatus.Generating;
            await _repository.UpdateAsync(lesson, ct);
        }

        try { await _jobs.EnqueueAsync(id, _lessons.CurrentTeacherId, sectionType.Value, key, ct); }
        catch { await _usage.ReleaseReservationsAsync(key, ct); throw; }
        return Accepted(new { status = "generating" });
    }

    private static string MapOperation(int section) => section switch
    {
        ContentTypes.LessonPlan => "lesson-plan", ContentTypes.EducationalContent => "content",
        ContentTypes.Activity => "activity", ContentTypes.Quiz => "quiz",
        ContentTypes.ImagePrompt => "image-prompt", ContentTypes.VideoPrompt => "video-prompt", _ => "other"
    };

    /// <summary>Saves teacher edits of one section payload (JSON as returned by GET lesson).</summary>
    [HttpPut("{id:int}/sections/{section}")]
    public async Task<IActionResult> SaveSection(int id, string section, [FromBody] SectionEditRequest request, CancellationToken ct)
    {
        var sectionType = SectionCode(section);
        if (sectionType is null) return BadRequest(new { error = "نوع محتوای نامعتبر است." });

        var result = await _sections.SaveSectionEditAsync(id, sectionType.Value, request.PayloadJson, ct);
        if (!result.Ok) return BadRequest(new { error = result.Error });

        var lesson = await _repository.GetForTeacherAsync(id, _lessons.CurrentTeacherId, includePackage: true, ct);
        return Ok(lesson!.ToPackageDto());
    }

    private static int? SectionCode(string section) => section switch
    {
        "lesson-plan" => ContentTypes.LessonPlan,
        "content" => ContentTypes.EducationalContent,
        "activity" => ContentTypes.Activity,
        "quiz" => ContentTypes.Quiz,
        "image-prompt" => ContentTypes.ImagePrompt,
        "video-prompt" => ContentTypes.VideoPrompt,
        _ => null,
    };
}

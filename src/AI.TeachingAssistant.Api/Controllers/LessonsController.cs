using AI.TeachingAssistant.Application.Dtos;
using AI.TeachingAssistant.Application.Interfaces;
using AI.TeachingAssistant.Application.Mapping;
using AI.TeachingAssistant.Application.Services;
using AI.TeachingAssistant.Domain.Entities;
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

    public LessonsController(
        LessonService lessons,
        PackageSectionService sections,
        IGenerationJobQueue jobs,
        ILessonRepository repository)
    {
        _lessons = lessons;
        _sections = sections;
        _jobs = jobs;
        _repository = repository;
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
        var lesson = await _repository.GetByIdAsync(id, includePackage: true, ct);
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
        var lesson = await _repository.GetByIdAsync(id, includePackage: false, ct);
        if (lesson is null) return NotFound(new { error = "درس یافت نشد." });
        _jobs.Enqueue(id, ContentTypes.All);
        return Accepted(new { status = "generating" });
    }

    /// <summary>Regenerates one section only; other sections are never touched (spec §8).</summary>
    [EnableRateLimiting("ai")]
    [HttpPost("{id:int}/generate/{section}")]
    public async Task<IActionResult> GenerateSection(int id, string section, CancellationToken ct)
    {
        var lesson = await _repository.GetByIdAsync(id, includePackage: false, ct);
        if (lesson is null) return NotFound(new { error = "درس یافت نشد." });

        var sectionType = SectionCode(section);
        if (sectionType is null) return BadRequest(new { error = "نوع محتوای نامعتبر است." });

        // Flip the status synchronously BEFORE the 202 (PART 18): the very first poll after
        // enqueue must see "generating", otherwise it can mistake the regenerate for an
        // instant no-op and show stale content. The worker flips it too, as a safety net.
        if (lesson.PackageStatus is not PackageStatus.Generating)
        {
            lesson.PackageStatus = PackageStatus.Generating;
            await _repository.UpdateAsync(lesson, ct);
        }

        _jobs.Enqueue(id, sectionType.Value);
        return Accepted(new { status = "generating" });
    }

    /// <summary>Saves teacher edits of one section payload (JSON as returned by GET lesson).</summary>
    [HttpPut("{id:int}/sections/{section}")]
    public async Task<IActionResult> SaveSection(int id, string section, [FromBody] SectionEditRequest request, CancellationToken ct)
    {
        var sectionType = SectionCode(section);
        if (sectionType is null) return BadRequest(new { error = "نوع محتوای نامعتبر است." });

        var result = await _sections.SaveSectionEditAsync(id, sectionType.Value, request.PayloadJson, ct);
        if (!result.Ok) return BadRequest(new { error = result.Error });

        var lesson = await _repository.GetByIdAsync(id, includePackage: true, ct);
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

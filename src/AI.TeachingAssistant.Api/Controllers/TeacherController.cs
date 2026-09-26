using AI.TeachingAssistant.Application.Dtos;
using AI.TeachingAssistant.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AI.TeachingAssistant.Api.Controllers;

[ApiController]
[Route("api/teacher")]
[Produces("application/json")]
public class TeacherController : ControllerBase
{
    private readonly TeacherService _teacher;

    public TeacherController(TeacherService teacher) => _teacher = teacher;

    [HttpGet]
    public async Task<ActionResult<TeacherDto>> Get(CancellationToken ct)
        => Ok(await _teacher.GetAsync(ct));

    [HttpPut]
    public async Task<ActionResult<TeacherDto>> Update([FromBody] UpdateTeacherRequest request, CancellationToken ct)
        => Ok(await _teacher.UpdateAsync(request, ct));
}

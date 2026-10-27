using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Application.Interfaces;
using AI.TeachingAssistant.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AI.TeachingAssistant.Api.Controllers;

[ApiController]
[Route("api/usage")]
public sealed class UsageController(IUsageService usage, ICurrentTeacher current) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var snapshot = await usage.GetAsync(current.TeacherId, ct);
        return Ok(new
        {
            text = new { limit = snapshot.TextLimit, used = snapshot.TextUsed, remaining = Math.Max(0, snapshot.TextLimit - snapshot.TextUsed) },
            image = new { limit = snapshot.ImageLimit, used = snapshot.ImageUsed, remaining = Math.Max(0, snapshot.ImageLimit - snapshot.ImageUsed) },
            resetsAtUtc = snapshot.ResetsAtUtc, warningPercent = snapshot.WarningPercent,
            operations = snapshot.Operations
        });
    }
}

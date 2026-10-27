using AI.TeachingAssistant.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace AI.TeachingAssistant.Api.Controllers;

/// <summary>Serves locally stored generated images by file name (see <see cref="IImageStore"/>).</summary>
[ApiController]
[Route("api/images")]
public class ImagesController : ControllerBase
{
    private readonly IImageStore _store;

    public ImagesController(IImageStore store) => _store = store;

    [HttpGet("{fileName}")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> Get(string fileName, CancellationToken ct)
    {
        var image = await _store.GetAsync(fileName, ct);
        if (image is null) return NotFound();

        // Stored bytes are immutable per file name (random id), so they are safe to cache hard.
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return File(image.Data, image.ContentType);
    }
}

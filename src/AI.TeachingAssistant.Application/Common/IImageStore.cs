namespace AI.TeachingAssistant.Application.Common;

/// <summary>Bytes read back from the image store, together with their content type.</summary>
public sealed record StoredImage(byte[] Data, string ContentType);

/// <summary>
/// Persists generated image bytes locally and reads them back for serving. The provider's
/// CDN URL is never relied on at render time; bytes are downloaded and stored so a lesson
/// survives reloads and the third-party link expiring.
/// </summary>
public interface IImageStore
{
    /// <summary>Saves bytes and returns the public, app-relative URL (e.g. <c>/api/images/&lt;name&gt;</c>).</summary>
    Task<string> SaveAsync(byte[] data, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Reads a previously stored image by its file name, or null when unknown or unsafe.</summary>
    Task<StoredImage?> GetAsync(string fileName, CancellationToken cancellationToken = default);
}

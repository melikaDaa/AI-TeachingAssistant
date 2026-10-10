using System.Text.RegularExpressions;
using AI.TeachingAssistant.Application.Common;

namespace AI.TeachingAssistant.Infrastructure.Storage;

/// <summary>
/// Stores generated image bytes on the local filesystem under a configured folder (default
/// <c>App_Data/generated-images</c>). Files are named with a random 32-hex id; reads validate
/// the name against that pattern so no request can escape the folder (path traversal safe).
/// </summary>
public sealed class FileImageStore : IImageStore
{
    private static readonly Regex SafeName =
        new("^[a-f0-9]{32}\\.(png|jpg|jpeg|webp)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly string _root;

    public FileImageStore(string? root)
    {
        _root = string.IsNullOrWhiteSpace(root)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "generated-images")
            : root;
    }

    public async Task<string> SaveAsync(byte[] data, string contentType, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);
        var name = Guid.NewGuid().ToString("N") + ExtensionFor(contentType);
        var path = Path.Combine(_root, name);
        await File.WriteAllBytesAsync(path, data, cancellationToken);
        // Public, app-relative URL served by ImagesController.
        return "/api/images/" + name;
    }

    public async Task<StoredImage?> GetAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !SafeName.IsMatch(fileName))
            return null;

        var path = Path.Combine(_root, fileName);
        if (!File.Exists(path))
            return null;

        var data = await File.ReadAllBytesAsync(path, cancellationToken);
        return new StoredImage(data, ContentTypeFor(Path.GetExtension(fileName)));
    }

    private static string ExtensionFor(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/webp" => ".webp",
            _ => ".png",
        };

    private static string ContentTypeFor(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "image/png",
        };
}

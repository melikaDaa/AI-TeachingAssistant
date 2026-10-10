using AI.TeachingAssistant.Application.Common;

namespace AI.TeachingAssistant.Tests;

/// <summary>Enabled in-memory image provider for pipeline tests (no network, no cost).</summary>
internal sealed class FakeImageProvider : IImageProvider
{
    public int Calls { get; private set; }
    public int ImagesPerSection { get; init; } = 2;

    public string Name => "Fake image provider";
    public bool IsEnabled => true;
    public int MaxImagesPerSection => ImagesPerSection;

    public Task<GeneratedImage> GenerateAsync(ImageGenerationRequest request, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(new GeneratedImage(new byte[] { 1, 2, 3, 4, 5 }, "image/png"));
    }
}

/// <summary>In-memory image store so tests never touch the filesystem.</summary>
internal sealed class InMemoryImageStore : IImageStore
{
    private readonly Dictionary<string, StoredImage> _files = new();

    public Task<string> SaveAsync(byte[] data, string contentType, CancellationToken cancellationToken = default)
    {
        var name = Guid.NewGuid().ToString("N") + ".png";
        _files[name] = new StoredImage(data, contentType);
        return Task.FromResult("/api/images/" + name);
    }

    public Task<StoredImage?> GetAsync(string fileName, CancellationToken cancellationToken = default) =>
        Task.FromResult(_files.TryGetValue(fileName, out var image) ? image : null);
}

using AI.TeachingAssistant.Infrastructure.Storage;
using Xunit;

namespace AI.TeachingAssistant.Tests;

public sealed class FileImageStoreTests
{
    [Fact]
    public async Task SavesAndReadsImageFromConfiguredDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "madreseai-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileImageStore(root);
            var bytes = new byte[] { 1, 2, 3 };
            var url = await store.SaveAsync(bytes, "image/png");
            var image = await store.GetAsync(Path.GetFileName(url));

            Assert.StartsWith("/api/images/", url);
            Assert.NotNull(image);
            Assert.Equal(bytes, image!.Data);
            Assert.True(Directory.Exists(root));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}

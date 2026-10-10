using Korkut.Core;
using Korkut.Documents.Storage;

namespace Korkut.TestSupport;

/// <summary>A filesystem object store in a throwaway directory, the same store a self-hosted install uses.</summary>
public sealed class ObjectStoreFixture : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "korkut-objects-" + Guid.NewGuid().ToString("N"));

    public ObjectUrlSigner Signer { get; } = new("korkut-integration-test-signing-secret-32b+", SystemClock.Instance);

    public FileSystemObjectStoreService StoreService { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        StoreService = new FileSystemObjectStoreService(_root, Signer);
        await StoreService.EnsureBucketsAsync();
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        return Task.CompletedTask;
    }
}

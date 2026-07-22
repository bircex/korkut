using Korkut.Infrastructure.Storage;
using Minio;
using Testcontainers.Minio;

namespace Korkut.Infrastructure.Tests;

/// <summary>Spins a real MinIO container and provides a client + object store.</summary>
public sealed class MinioFixture : IAsyncLifetime
{
    private readonly MinioContainer _container = new MinioBuilder("minio/minio:RELEASE.2025-04-08T15-41-24Z").Build();

    public IMinioClient Client { get; private set; } = null!;

    public MinioObjectStoreService StoreService { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var endpoint = new Uri(_container.GetConnectionString());
        Client = new MinioClient()
            .WithEndpoint(endpoint.Host, endpoint.Port)
            .WithCredentials(_container.GetAccessKey(), _container.GetSecretKey())
            .WithSSL(false)
            .Build();
        StoreService = new MinioObjectStoreService(Client);
        await StoreService.EnsureBucketsAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

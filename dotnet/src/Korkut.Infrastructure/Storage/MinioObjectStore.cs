using System.Runtime.CompilerServices;
using Korkut.Application.Documents;
using Minio;
using Minio.DataModel.Args;

namespace Korkut.Infrastructure.Storage;

/// <summary>MinIO-backed <see cref="IObjectStore"/>. Buckets are created private (no public policy).</summary>
public sealed class MinioObjectStore(IMinioClient client) : IObjectStore
{
    public async Task EnsureBucketsAsync(CancellationToken ct = default)
    {
        foreach (var bucket in new[] { ObjectBuckets.Quarantine, ObjectBuckets.Clean })
        {
            var exists = await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct);
            if (!exists)
            {
                await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);
            }
        }
    }

    public async Task PutAsync(string bucket, string key, Stream data, long size, string contentType, CancellationToken ct = default)
        => await client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket).WithObject(key).WithStreamData(data).WithObjectSize(size).WithContentType(contentType), ct);

    public async Task<Stream> GetAsync(string bucket, string key, CancellationToken ct = default)
    {
        var buffer = new MemoryStream();
        await client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucket).WithObject(key)
            .WithCallbackStream(async (s, c) => await s.CopyToAsync(buffer, c)), ct);
        buffer.Position = 0;
        return buffer;
    }

    public async Task<bool> ExistsAsync(string bucket, string key, CancellationToken ct = default)
    {
        try
        {
            await client.StatObjectAsync(new StatObjectArgs().WithBucket(bucket).WithObject(key), ct);
            return true;
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            return false;
        }
    }

    public async Task CopyAsync(string fromBucket, string toBucket, string key, CancellationToken ct = default)
        => await client.CopyObjectAsync(new CopyObjectArgs()
            .WithBucket(toBucket).WithObject(key)
            .WithCopyObjectSource(new CopySourceObjectArgs().WithBucket(fromBucket).WithObject(key)), ct);

    public async Task RemoveAsync(string bucket, string key, CancellationToken ct = default)
        => await client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(key), ct);

    public async Task<string> PresignGetAsync(string bucket, string key, TimeSpan expiry, CancellationToken ct = default)
        => await client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(bucket).WithObject(key).WithExpiry((int)expiry.TotalSeconds));

    public async IAsyncEnumerable<string> ListKeysAsync(string bucket, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var args = new ListObjectsArgs().WithBucket(bucket).WithRecursive(true);
        await foreach (var item in client.ListObjectsEnumAsync(args, ct))
        {
            yield return item.Key;
        }
    }
}

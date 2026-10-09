using System.Runtime.CompilerServices;
using Korkut.Contract.Documents.Storage;

namespace Korkut.Documents.Storage;

/// <summary>
/// <see cref="IObjectStoreService"/> on a local directory, the default for a self-hosted
/// install: one office's documents on a volume, no object-storage server to run. Each bucket is a
/// directory under the root; an object's content type sits beside it in a <c>.type</c> file so a
/// download is served the way it was uploaded. Writes go to a temporary file first and are moved
/// into place, so a reader never sees half an object.
/// </summary>
public sealed class FileSystemObjectStoreService(string root, ObjectUrlSigner signer) : IObjectStoreService
{
    private const string TypeSuffix = ".type";
    private const string PartialSuffix = ".partial";

    private readonly string _root = Path.GetFullPath(root);

    public Task EnsureBucketsAsync(CancellationToken ct = default)
    {
        foreach (var bucket in new[] { ObjectBuckets.Quarantine, ObjectBuckets.Clean })
        {
            Directory.CreateDirectory(BucketPath(bucket));
        }

        return Task.CompletedTask;
    }

    public async Task PutAsync(string bucket, string key, Stream data, long size, string contentType, CancellationToken ct = default)
    {
        var path = ObjectPath(bucket, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var partial = path + "." + Guid.NewGuid().ToString("N") + PartialSuffix;
        try
        {
            await using (var file = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await data.CopyToAsync(file, ct);
            }

            await File.WriteAllTextAsync(path + TypeSuffix, contentType, ct);
            File.Move(partial, path, overwrite: true);
        }
        finally
        {
            File.Delete(partial);
        }
    }

    public Task<Stream> GetAsync(string bucket, string key, CancellationToken ct = default)
    {
        var path = ObjectPath(bucket, key);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No object '{key}' in bucket '{bucket}'.");
        }

        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));
    }

    public Task<bool> ExistsAsync(string bucket, string key, CancellationToken ct = default)
        => Task.FromResult(File.Exists(ObjectPath(bucket, key)));

    public Task CopyAsync(string fromBucket, string toBucket, string key, CancellationToken ct = default)
    {
        var from = ObjectPath(fromBucket, key);
        var to = ObjectPath(toBucket, key);
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        var partial = to + "." + Guid.NewGuid().ToString("N") + PartialSuffix;
        try
        {
            File.Copy(from, partial);
            if (File.Exists(from + TypeSuffix))
            {
                File.Copy(from + TypeSuffix, to + TypeSuffix, overwrite: true);
            }

            File.Move(partial, to, overwrite: true);
        }
        finally
        {
            File.Delete(partial);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string bucket, string key, CancellationToken ct = default)
    {
        var path = ObjectPath(bucket, key);
        File.Delete(path);
        File.Delete(path + TypeSuffix);
        return Task.CompletedTask;
    }

    public Task<string> PresignGetAsync(string bucket, string key, TimeSpan expiry, CancellationToken ct = default)
    {
        ObjectPath(bucket, key); // rejects a name that could not be stored, before it is signed
        return Task.FromResult(signer.Sign(bucket, key, expiry));
    }

    public async IAsyncEnumerable<string> ListKeysAsync(string bucket, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var bucketPath = BucketPath(bucket);
        if (!Directory.Exists(bucketPath))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(bucketPath, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            if (file.EndsWith(TypeSuffix, StringComparison.Ordinal) || file.EndsWith(PartialSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            yield return Path.GetRelativePath(bucketPath, file).Replace(Path.DirectorySeparatorChar, '/');
        }

        await Task.CompletedTask;
    }

    /// <summary>The content type an object was stored with, for serving it.</summary>
    public string ContentTypeOf(string bucket, string key)
    {
        var typePath = ObjectPath(bucket, key) + TypeSuffix;
        return File.Exists(typePath) ? File.ReadAllText(typePath).Trim() : "application/octet-stream";
    }

    private string BucketPath(string bucket)
    {
        if (string.IsNullOrEmpty(bucket) || !bucket.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
        {
            throw new ArgumentException($"Invalid bucket name '{bucket}'.", nameof(bucket));
        }

        return Path.Combine(_root, bucket);
    }

    // Keys are opaque ("ab/<uuid>"), but they still reach the filesystem, so anything that could
    // step outside the bucket (.., an absolute path, a backslash) is refused rather than resolved.
    private string ObjectPath(string bucket, string key)
    {
        var bucketPath = BucketPath(bucket);
        if (string.IsNullOrEmpty(key) || key.Contains('\\') || key.StartsWith('/')
            || key.Split('/').Any(segment => segment is "" or "." or "..")
            || key.EndsWith(TypeSuffix, StringComparison.Ordinal) || key.EndsWith(PartialSuffix, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Invalid object key '{key}'.", nameof(key));
        }

        var path = Path.GetFullPath(Path.Combine(bucketPath, key));
        if (!path.StartsWith(bucketPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Invalid object key '{key}'.", nameof(key));
        }

        return path;
    }
}

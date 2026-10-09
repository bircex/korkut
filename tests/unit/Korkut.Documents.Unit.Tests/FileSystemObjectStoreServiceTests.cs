using System.Text;
using Korkut.Core;
using Korkut.Documents.Storage;

namespace Korkut.Documents.Tests;

/// <summary>
/// The filesystem store a self-hosted install keeps its documents in, and the signed links that
/// stand in for S3 presigned URLs: round-trips, what a key may not do on disk, and when a link
/// stops opening anything.
/// </summary>
public sealed class FileSystemObjectStoreServiceTests : IDisposable
{
    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.Parse("2026-10-10T09:00:00Z");
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "korkut-objects-" + Guid.NewGuid().ToString("N"));
    private readonly Clock _clock = new();
    private readonly ObjectUrlSigner _signer;
    private readonly FileSystemObjectStoreService _store;

    public FileSystemObjectStoreServiceTests()
    {
        _signer = new ObjectUrlSigner("unit-test-secret", _clock);
        _store = new FileSystemObjectStoreService(_root, _signer);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text));

    private async Task<string> ReadAsync(string bucket, string key)
    {
        await using var stream = await _store.GetAsync(bucket, key);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task An_object_round_trips_and_keeps_its_content_type()
    {
        await _store.EnsureBucketsAsync();
        var key = ObjectBuckets.NewKey();

        await _store.PutAsync(ObjectBuckets.Quarantine, key, Bytes("dilekçe"), 9, "application/pdf");

        Assert.True(await _store.ExistsAsync(ObjectBuckets.Quarantine, key));
        Assert.Equal("dilekçe", await ReadAsync(ObjectBuckets.Quarantine, key));
        Assert.Equal("application/pdf", _store.ContentTypeOf(ObjectBuckets.Quarantine, key));
    }

    [Fact]
    public async Task Copy_promotes_bytes_and_type_and_remove_deletes_both()
    {
        var key = ObjectBuckets.NewKey();
        await _store.PutAsync(ObjectBuckets.Quarantine, key, Bytes("temiz"), 5, "text/plain");

        await _store.CopyAsync(ObjectBuckets.Quarantine, ObjectBuckets.Clean, key);
        await _store.RemoveAsync(ObjectBuckets.Quarantine, key);

        Assert.False(await _store.ExistsAsync(ObjectBuckets.Quarantine, key));
        Assert.Equal("temiz", await ReadAsync(ObjectBuckets.Clean, key));
        Assert.Equal("text/plain", _store.ContentTypeOf(ObjectBuckets.Clean, key));
    }

    [Fact]
    public async Task Listing_returns_object_keys_only()
    {
        var a = ObjectBuckets.NewKey();
        var b = ObjectBuckets.NewKey();
        await _store.PutAsync(ObjectBuckets.Clean, a, Bytes("a"), 1, "text/plain");
        await _store.PutAsync(ObjectBuckets.Clean, b, Bytes("b"), 1, "text/plain");

        var keys = new List<string>();
        await foreach (var key in _store.ListKeysAsync(ObjectBuckets.Clean))
        {
            keys.Add(key);
        }

        Assert.Equal(new[] { a, b }.Order(), keys.Order());
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("ab/../../escape")]
    [InlineData("/etc/passwd")]
    [InlineData("ab\\evil")]
    [InlineData("ab//evil")]
    [InlineData("ab/x.type")]
    public async Task A_key_that_could_leave_its_bucket_is_refused(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _store.PutAsync(ObjectBuckets.Clean, key, Bytes("x"), 1, "text/plain"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => _store.PresignGetAsync(ObjectBuckets.Clean, key, TimeSpan.FromMinutes(1)));
    }

    [Theory]
    [InlineData("../objects")]
    [InlineData("Upper")]
    [InlineData("")]
    public async Task A_bucket_outside_the_naming_rules_is_refused(string bucket)
        => await Assert.ThrowsAsync<ArgumentException>(() => _store.ExistsAsync(bucket, "ab/cd"));

    [Fact]
    public async Task A_signed_link_names_its_object_until_it_expires()
    {
        var key = ObjectBuckets.NewKey();
        var url = await _store.PresignGetAsync(ObjectBuckets.Clean, key, TimeSpan.FromMinutes(2));
        var token = url[ObjectUrlSigner.PathPrefix.Length..];

        Assert.Equal((ObjectBuckets.Clean, key), _signer.Verify(token));

        _clock.UtcNow = _clock.UtcNow.AddMinutes(3);
        Assert.Null(_signer.Verify(token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-dot")]
    [InlineData("!!!.!!!")]
    public void A_malformed_token_opens_nothing(string token) => Assert.Null(_signer.Verify(token));

    [Fact]
    public async Task A_token_signed_with_another_secret_opens_nothing()
    {
        var other = new FileSystemObjectStoreService(_root, new ObjectUrlSigner("another-secret", _clock));
        var url = await other.PresignGetAsync(ObjectBuckets.Clean, ObjectBuckets.NewKey(), TimeSpan.FromMinutes(2));

        Assert.Null(_signer.Verify(url[ObjectUrlSigner.PathPrefix.Length..]));
    }
}

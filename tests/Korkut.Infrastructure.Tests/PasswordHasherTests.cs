using Korkut.Infrastructure.Security;

namespace Korkut.Infrastructure.Tests;

/// <summary>Pure unit tests for the PBKDF2 hasher (no database).</summary>
public class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new(iterations: 10_000);

    [Fact]
    public void Hash_is_versioned_and_not_the_plaintext()
    {
        var hash = _hasher.Hash("s3cret-password");
        Assert.StartsWith("pbkdf2-sha256$v1$", hash);
        Assert.DoesNotContain("s3cret-password", hash, StringComparison.Ordinal);
    }

    [Fact]
    public void Correct_password_verifies_and_wrong_does_not()
    {
        var hash = _hasher.Hash("correct");
        Assert.True(_hasher.Verify("correct", hash).Verified);
        Assert.False(_hasher.Verify("wrong", hash).Verified);
    }

    [Fact]
    public void Two_hashes_of_the_same_password_differ_by_salt()
    {
        Assert.NotEqual(_hasher.Hash("same"), _hasher.Hash("same"));
    }

    [Fact]
    public void A_hash_with_fewer_iterations_is_flagged_for_rehash()
    {
        var weak = new Pbkdf2PasswordHasher(iterations: 5_000).Hash("pw");
        var (verified, needsRehash) = new Pbkdf2PasswordHasher(iterations: 10_000).Verify("pw", weak);
        Assert.True(verified);
        Assert.True(needsRehash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-valid-hash")]
    [InlineData("pbkdf2-sha256$v1$abc$def")]
    public void Malformed_hashes_fail_closed(string encoded)
    {
        Assert.False(_hasher.Verify("pw", encoded).Verified);
    }
}

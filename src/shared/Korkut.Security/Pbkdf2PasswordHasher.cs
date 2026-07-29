using System.Security.Cryptography;
using Korkut.Core;

namespace Korkut.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing (the built-in, established KDF). The encoded
/// form is self-describing so parameters can be raised over time and old hashes
/// upgraded on next login:
///
///   <c>pbkdf2-sha256$v1$&lt;iterations&gt;$&lt;salt-b64&gt;$&lt;hash-b64&gt;</c>
///
/// Verification is constant-time. No cryptographic primitive is implemented here.
/// </summary>
public sealed class Pbkdf2PasswordHasher(int iterations = 210_000) : IPasswordHasher
{
    private const string Prefix = "pbkdf2-sha256$v1";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private static readonly HashAlgorithmName Algo = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algo, KeySize);
        return $"{Prefix}${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public (bool Verified, bool NeedsRehash) Verify(string password, string encodedHash)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (string.IsNullOrEmpty(encodedHash))
        {
            return (false, false);
        }

        var parts = encodedHash.Split('$');
        // pbkdf2-sha256 | v1 | iterations | salt | hash
        if (parts.Length != 5 || parts[0] != "pbkdf2-sha256" || parts[1] != "v1"
            || !int.TryParse(parts[2], out var storedIterations))
        {
            return (false, false);
        }

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expected = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return (false, false);
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, storedIterations, Algo, expected.Length);
        var verified = CryptographicOperations.FixedTimeEquals(actual, expected);
        var needsRehash = verified && storedIterations < iterations;
        return (verified, needsRehash);
    }
}

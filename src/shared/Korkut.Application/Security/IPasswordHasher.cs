namespace Korkut.Application.Security;

/// <summary>
/// Versioned, adaptive password hashing. Implementations use an established KDF
/// (never a hand-rolled primitive) and embed their parameters so the cost can be
/// raised over time and old hashes upgraded on next successful login.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hash a password into a self-describing, versioned string.</summary>
    string Hash(string password);

    /// <summary>Constant-time verify; also reports whether the stored hash should be upgraded.</summary>
    (bool Verified, bool NeedsRehash) Verify(string password, string encodedHash);
}

using System.Security.Cryptography;
using Korkut.Application.Security;

namespace Korkut.Infrastructure.Security;

/// <summary>
/// Opaque reset tokens: 256 bits of CSPRNG entropy, base64url-encoded for delivery,
/// and stored only as their SHA-256 hash (a lookup on a high-entropy value needs no
/// salt/KDF). The raw token never touches the database, logs or audit.
/// </summary>
public sealed class Sha256TokenHasher : ITokenHasher
{
    public string NewRawToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public string Hash(string rawToken)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawToken);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

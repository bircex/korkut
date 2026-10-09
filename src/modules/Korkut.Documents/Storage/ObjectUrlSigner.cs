using System.Security.Cryptography;
using System.Text;
using Korkut.Core;
using Microsoft.AspNetCore.WebUtilities;

namespace Korkut.Documents.Storage;

/// <summary>
/// The filesystem store's stand-in for an S3 presigned URL: a short-lived, tamper-proof token
/// naming one object, served by the app itself at <see cref="PathPrefix"/>. Like a presigned URL
/// it is a bearer capability: whoever holds it until it expires can read that one object, so the
/// caller must have checked permission before minting it.
/// </summary>
public sealed class ObjectUrlSigner
{
    public const string PathPrefix = "/api/documents/files/";

    private readonly byte[] _key;
    private readonly IClock _clock;

    public ObjectUrlSigner(string secret, IClock clock)
    {
        if (string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException("KORKUT_SECRET is required to sign document download links.");
        }

        // A key of its own, derived from the host secret, so a download token can never be
        // mistaken for (or forged from) anything else signed with KORKUT_SECRET.
        _key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), "korkut:object-url:v1"u8);
        _clock = clock;
    }

    public string Sign(string bucket, string key, TimeSpan lifetime)
    {
        var expires = _clock.UtcNow.Add(lifetime).ToUnixTimeSeconds();
        var payload = Encoding.UTF8.GetBytes($"{bucket}\n{key}\n{expires}");
        return PathPrefix + WebEncoders.Base64UrlEncode(payload) + "." + WebEncoders.Base64UrlEncode(Mac(payload));
    }

    /// <summary>The object a token names, or null when it is malformed, forged or expired.</summary>
    public (string Bucket, string Key)? Verify(string token)
    {
        var dot = token.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0)
        {
            return null;
        }

        byte[] payload, mac;
        try
        {
            payload = WebEncoders.Base64UrlDecode(token[..dot]);
            mac = WebEncoders.Base64UrlDecode(token[(dot + 1)..]);
        }
        catch (FormatException)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(mac, Mac(payload)))
        {
            return null;
        }

        var parts = Encoding.UTF8.GetString(payload).Split('\n');
        if (parts.Length != 3 || !long.TryParse(parts[2], out var expires)
            || _clock.UtcNow.ToUnixTimeSeconds() > expires)
        {
            return null;
        }

        return (parts[0], parts[1]);
    }

    private byte[] Mac(byte[] payload) => HMACSHA256.HashData(_key, payload);
}

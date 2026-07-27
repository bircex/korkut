using Korkut.Core;
using System.Security.Cryptography;
using Korkut.Application.Security;

namespace Korkut.Infrastructure.Security;

/// <summary>
/// AES-256-GCM envelope encryption for at-rest secrets. Encoded form:
/// <c>v&lt;keyId&gt;$&lt;nonce&gt;$&lt;ciphertext&gt;$&lt;tag&gt;</c> (base64) — the key id lets the
/// wrapping key be rotated without losing old ciphertexts. The master key is
/// operator-supplied (KDM-033 binds it from env); never hard-coded or logged.
/// </summary>
public sealed class AesGcmSecretEncryptor(byte[] masterKey, int keyId = 1) : ISecretEncryptor
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public string Encrypt(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(masterKey, TagSize);
        aes.Encrypt(nonce, plaintext, cipher, tag);
        return $"v{keyId}${B64(nonce)}${B64(cipher)}${B64(tag)}";
    }

    public byte[] Decrypt(string encoded)
    {
        var parts = encoded.Split('$');
        if (parts.Length != 4 || !parts[0].StartsWith('v'))
        {
            throw new FormatException("Malformed encrypted secret.");
        }

        var nonce = Convert.FromBase64String(parts[1]);
        var cipher = Convert.FromBase64String(parts[2]);
        var tag = Convert.FromBase64String(parts[3]);
        var plaintext = new byte[cipher.Length];
        using var aes = new AesGcm(masterKey, TagSize);
        aes.Decrypt(nonce, cipher, tag, plaintext);
        return plaintext;
    }

    private static string B64(byte[] b) => Convert.ToBase64String(b);
}

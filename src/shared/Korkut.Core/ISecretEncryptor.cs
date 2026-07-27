namespace Korkut.Core;

/// <summary>
/// Envelope encryption for at-rest secrets (e.g. TOTP seeds). The encoded form is
/// self-describing and versioned so the wrapping key can be rotated. Implementations
/// use an established AEAD (never a rolled primitive) and never log plaintext.
/// </summary>
public interface ISecretEncryptor
{
    string Encrypt(byte[] plaintext);

    byte[] Decrypt(string encoded);
}

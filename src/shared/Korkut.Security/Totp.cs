using System.Security.Cryptography;

namespace Korkut.Security;

/// <summary>
/// RFC 6238 TOTP (HMAC-SHA1, 30s step, 6 digits) over the built-in HMAC primitive —
/// no rolled crypto. Verification accepts a small clock window and returns the
/// matched step so the caller can reject replay of an already-used code.
/// </summary>
public static class Totp
{
    public const int Digits = 6;
    public const int StepSeconds = 30;

    public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(20); // 160-bit

    public static string Base32Secret(byte[] secret) => Base32.Encode(secret);

    /// <summary>otpauth:// provisioning URI for authenticator apps.</summary>
    public static string ProvisioningUri(byte[] secret, string issuer, string account)
    {
        var s = Base32.Encode(secret);
        return $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}"
             + $"?secret={s}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
    }

    public static string Compute(byte[] secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counter, step);
        // RFC 6238 mandates HMAC-SHA1 for TOTP; authenticator apps (Google Authenticator,
        // Authy, ...) interoperate on SHA1. This is protocol-required, not a weakness.
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
        var hash = HMACSHA1.HashData(secret, counter);
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);
        var code = binary % (int)Math.Pow(10, Digits);
        return code.ToString().PadLeft(Digits, '0');
    }

    public static long CurrentStep(DateTimeOffset now) => now.ToUnixTimeSeconds() / StepSeconds;

    /// <summary>
    /// Verify a code within ±<paramref name="window"/> steps. Returns the matched step
    /// (constant-time compare) or null. Callers must reject a step already consumed.
    /// </summary>
    public static long? Verify(byte[] secret, string code, DateTimeOffset now, int window = 1)
    {
        var current = CurrentStep(now);
        for (var i = -window; i <= window; i++)
        {
            var step = current + i;
            var expected = Compute(secret, step);
            if (CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(expected),
                    System.Text.Encoding.ASCII.GetBytes(code)))
            {
                return step;
            }
        }

        return null;
    }
}

/// <summary>Minimal RFC 4648 Base32 (no padding) for otpauth secrets.</summary>
internal static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(byte[] data)
    {
        var sb = new System.Text.StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                sb.Append(Alphabet[(buffer >> bits) & 31]);
            }
        }

        if (bits > 0)
        {
            sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        }

        return sb.ToString();
    }
}

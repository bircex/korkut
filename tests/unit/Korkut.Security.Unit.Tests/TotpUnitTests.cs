using System.Security.Cryptography;

namespace Korkut.Security.Unit.Tests;

/// <summary>Unit tests for the TOTP algorithm and the AES-GCM envelope encryptor (no DB).</summary>
public class TotpUnitTests
{
    [Fact]
    public void Generated_code_verifies_within_the_current_window()
    {
        var secret = Totp.NewSecret();
        var now = DateTimeOffset.UnixEpoch.AddYears(56);
        var code = Totp.Compute(secret, Totp.CurrentStep(now));

        Assert.NotNull(Totp.Verify(secret, code, now));
    }

    [Fact]
    public void Code_from_a_far_step_is_rejected()
    {
        var secret = Totp.NewSecret();
        var now = DateTimeOffset.UnixEpoch.AddYears(56);
        var stale = Totp.Compute(secret, Totp.CurrentStep(now) - 5); // 5 steps ago, outside window

        Assert.Null(Totp.Verify(secret, stale, now, window: 1));
    }

    [Fact]
    public void Adjacent_step_is_accepted_within_window()
    {
        var secret = Totp.NewSecret();
        var now = DateTimeOffset.UnixEpoch.AddYears(56);
        var prev = Totp.Compute(secret, Totp.CurrentStep(now) - 1);

        var matched = Totp.Verify(secret, prev, now, window: 1);
        Assert.Equal(Totp.CurrentStep(now) - 1, matched);
    }

    [Fact]
    public void Provisioning_uri_is_well_formed()
    {
        var uri = Totp.ProvisioningUri(Totp.NewSecret(), "Korkut", "lawyer@office.test");
        Assert.StartsWith("otpauth://totp/Korkut:", uri);
        Assert.Contains("algorithm=SHA1", uri);
        Assert.Contains("digits=6", uri);
    }

    [Fact]
    public void Envelope_encrypt_round_trips_and_hides_plaintext()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var enc = new AesGcmSecretEncryptor(key);
        var secret = Totp.NewSecret();

        var encoded = enc.Encrypt(secret);
        Assert.StartsWith("v1$", encoded);
        Assert.DoesNotContain(Convert.ToBase64String(secret), encoded, StringComparison.Ordinal);
        Assert.Equal(secret, enc.Decrypt(encoded));
    }

    [Fact]
    public void Tampered_ciphertext_fails_authentication()
    {
        var enc = new AesGcmSecretEncryptor(RandomNumberGenerator.GetBytes(32));
        var encoded = enc.Encrypt(Totp.NewSecret());
        var parts = encoded.Split('$');
        var bytes = Convert.FromBase64String(parts[2]);
        bytes[0] ^= 0xFF; // flip a ciphertext bit
        parts[2] = Convert.ToBase64String(bytes);
        Assert.Throws<AuthenticationTagMismatchException>(() => enc.Decrypt(string.Join('$', parts)));
    }
}

using Korkut.Domain.Redaction;

namespace Korkut.Domain.Tests;

/// <summary>
/// Proves the central redactor masks secrets/PII. Canary values are assembled at
/// runtime (never committed as literals) so this file itself stays free of
/// secret-shaped strings.
/// </summary>
public class RedactionTests
{
    // Runtime-assembled canaries (avoid committing matchable secret literals).
    public static TheoryData<string> SensitiveValues()
    {
        var data = new TheoryData<string>();
        data.Add("sk-ant-" + new string('a', 28));            // anthropic key
        data.Add("sk-" + new string('b', 30));               // openai key
        data.Add("AIza" + new string('c', 30));              // google key
        data.Add("eyJ" + new string('h', 12) + "." + new string('d', 20) + "." + new string('e', 20)); // jwt
        data.Add("Bearer " + new string('f', 24));           // bearer token
        data.Add("password = " + new string('g', 18));       // assigned secret
        data.Add(new string('1', 11));                       // TCKN (11 digits)
        data.Add("muvekkil" + "@example.test");              // email
        return data;
    }

    [Theory]
    [MemberData(nameof(SensitiveValues))]
    public void Sensitive_values_are_detected_and_masked(string secret)
    {
        var line = $"processing {secret} now";

        Assert.True(SensitiveDataRedactor.ContainsSensitive(secret));
        var redacted = SensitiveDataRedactor.Redact(line);
        Assert.DoesNotContain(secret, redacted, StringComparison.Ordinal);
        Assert.Contains(SensitiveDataRedactor.Mask, redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Non_sensitive_text_is_unchanged()
    {
        const string safe = "matter opened for office 7 with status open";

        Assert.False(SensitiveDataRedactor.ContainsSensitive(safe));
        Assert.Equal(safe, SensitiveDataRedactor.Redact(safe));
    }

    [Fact]
    public void Null_and_empty_are_safe()
    {
        Assert.Equal(string.Empty, SensitiveDataRedactor.Redact(null));
        Assert.Equal(string.Empty, SensitiveDataRedactor.Redact(string.Empty));
        Assert.False(SensitiveDataRedactor.ContainsSensitive(null));
    }
}

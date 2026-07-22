using System.Text.RegularExpressions;

namespace Korkut.Domain.Redaction;

/// <summary>
/// Central redaction for logs/traces/metrics. Scrubs values that must never leave
/// the process in telemetry: provider/model API keys, bearer/JWT credentials,
/// passwords and secrets in assignments, and Turkish legal PII (TCKN national ids,
/// IBANs, e-mail addresses). Pure and dependency-free so it is trivially testable
/// and reusable by the log processor and any manual scrub site.
///
/// Redaction is deliberately conservative-to-aggressive on secrets: a false
/// positive (over-redaction) is acceptable; a leaked key or national id is not.
/// </summary>
public static partial class SensitiveDataRedactor
{
    public const string Mask = "[REDACTED]";

    private static readonly (string Name, Regex Pattern)[] Rules =
    [
        ("anthropic-key", AnthropicKey()),
        ("openai-key", OpenAiKey()),
        ("google-key", GoogleKey()),
        ("aws-key", AwsKey()),
        ("jwt", Jwt()),
        ("bearer", Bearer()),
        ("assigned-secret", AssignedSecret()),
        ("tckn", Tckn()),
        ("iban", Iban()),
        ("email", Email()),
    ];

    /// <summary>Return <paramref name="input"/> with every sensitive span masked.</summary>
    public static string Redact(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input ?? string.Empty;
        }

        var result = input;
        foreach (var (_, pattern) in Rules)
        {
            result = pattern.Replace(result, Mask);
        }

        return result;
    }

    /// <summary>True if <paramref name="input"/> contains any sensitive value.</summary>
    public static bool ContainsSensitive(string? input) =>
        !string.IsNullOrEmpty(input) && Rules.Any(r => r.Pattern.IsMatch(input));

    [GeneratedRegex(@"sk-ant-[A-Za-z0-9_-]{16,}", RegexOptions.CultureInvariant)]
    private static partial Regex AnthropicKey();

    [GeneratedRegex(@"sk-[A-Za-z0-9]{20,}", RegexOptions.CultureInvariant)]
    private static partial Regex OpenAiKey();

    [GeneratedRegex(@"AIza[A-Za-z0-9_-]{20,}", RegexOptions.CultureInvariant)]
    private static partial Regex GoogleKey();

    [GeneratedRegex(@"AKIA[0-9A-Z]{16}", RegexOptions.CultureInvariant)]
    private static partial Regex AwsKey();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]{6,}\.[A-Za-z0-9_-]{6,}\.[A-Za-z0-9_-]{4,}", RegexOptions.CultureInvariant)]
    private static partial Regex Jwt();

    [GeneratedRegex(@"(?i)bearer\s+[A-Za-z0-9._~+/-]{12,}=*", RegexOptions.CultureInvariant)]
    private static partial Regex Bearer();

    // KEY/SECRET/PASSWORD/TOKEN = <value> (quoted or not).
    [GeneratedRegex(
        @"(?i)\b(?:api[_-]?key|apikey|secret|password|passwd|token)\b\s*[:=]\s*[""']?[A-Za-z0-9/+_.=-]{8,}",
        RegexOptions.CultureInvariant)]
    private static partial Regex AssignedSecret();

    // Turkish national id (TCKN): 11 digits, standalone.
    [GeneratedRegex(@"(?<!\d)\d{11}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex Tckn();

    // Turkish IBAN.
    [GeneratedRegex(@"(?i)TR\d{2}[ ]?(?:\d{4}[ ]?){5}\d{2}", RegexOptions.CultureInvariant)]
    private static partial Regex Iban();

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex Email();
}

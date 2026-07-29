namespace Korkut.Contract.Pii;

/// <summary>The kinds of Turkish PII the redactor recognizes.</summary>
public enum PiiKind
{
    /// <summary>T.C. Kimlik No — 11-digit national id (checksum-validated).</summary>
    Tckn,

    /// <summary>Turkish phone number (mobile / landline, with or without +90/0 prefix).</summary>
    Phone,

    /// <summary>E-mail address.</summary>
    Email,

    Iban,
}

/// <summary>
/// One redacted value: its <see cref="Kind"/>, the original <see cref="Value"/>, and the
/// <see cref="Placeholder"/> token that stands in for it in masked text. The same original value
/// always maps to the same placeholder within a redaction scope, so restore is unambiguous.
/// </summary>
public sealed record PiiMatch(PiiKind Kind, string Value, string Placeholder);

/// <summary>The result of masking a single text: the masked <see cref="Text"/> and the matches found.</summary>
public sealed record RedactionResult(string Text, IReadOnlyList<PiiMatch> Matches);

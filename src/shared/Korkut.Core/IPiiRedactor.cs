namespace Korkut.Core;

/// <summary>
/// Redacts Turkish PII (TCKN, phone, e-mail, IBAN) out of text before it leaves the office for a
/// cloud LLM (Agent chat-time) or is written to the vector index (Knowledge index-time), and
/// restores it in a reply. Critical for a legal product: raw PII must never reach a cloud model or
/// the search index. On-prem/local model endpoints skip redaction (their data never leaves).
/// </summary>
public interface IPiiRedactor
{
    /// <summary>Mask one standalone text; the result carries the matches needed to restore it.</summary>
    RedactionResult Redact(string text);

    /// <summary>
    /// Open a scope that shares ONE value→placeholder map across several masked texts (e.g. the
    /// system prompt + each chat message), so the same value gets the same placeholder everywhere
    /// and the reply can be restored against the union of matches.
    /// </summary>
    IPiiRedactionScope NewScope();
}

/// <summary>
/// A redaction scope with a shared placeholder map. Mask any number of texts; then restore a
/// reply (e.g. the model's output) by putting the original values back in place of their
/// placeholders. Not thread-safe — one scope per logical unit of work (e.g. one chat turn).
/// </summary>
public interface IPiiRedactionScope
{
    /// <summary>Mask <paramref name="text"/>, reusing placeholders already assigned in this scope.</summary>
    string Mask(string text);

    /// <summary>Replace this scope's placeholders in <paramref name="text"/> with their original values.</summary>
    string Restore(string text);

    /// <summary>Every match assigned in this scope so far.</summary>
    IReadOnlyCollection<PiiMatch> Matches { get; }
}

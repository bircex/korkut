namespace Korkut.Contract.Pii;

/// <summary>
/// Redacts Turkish PII (TCKN, phone, e-mail, IBAN) out of text before it leaves the office for a
/// cloud LLM (Agent chat-time) or is written to the vector index (Knowledge index-time), and
/// restores it in a reply. Critical for a legal product: raw PII must never reach a cloud model or
/// the search index. On-prem/local model endpoints skip redaction (their data never leaves).
/// </summary>
public interface IPiiRedactorService
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

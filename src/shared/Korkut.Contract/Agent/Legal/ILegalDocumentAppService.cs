namespace Korkut.Contract.Agent.Legal;

/// <summary>The full text of a public legal document, as the UI's sources drawer shows it.</summary>
public sealed record LegalDocumentResponse(string Source, string Id, string ContentMd);

/// <summary>
/// Full-text lookup of a public legal document (a <c>SourceRef</c>'s <c>source</c>+<c>id</c>) for
/// the UI's İlgili Kararlar drawer — the same source clients <c>fetch_document</c> uses, exposed
/// as an endpoint so the drawer can show a decision without a model turn.
/// </summary>
public interface ILegalDocumentAppService
{
    /// <summary>Fetch one document's text (Markdown). Throws NotFound when the source is unknown,
    /// cannot fetch, or has no such document.</summary>
    Task<LegalDocumentResponse> GetAsync(string source, string id, int? page = null, CancellationToken ct = default);
}

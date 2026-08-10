namespace Korkut.Contract.LawClients;

/// <summary>The full text of a public legal document, as the UI's İlgili Kararlar drawer shows it.</summary>
public sealed record LawClientsDocumentResponse(string Source, string Id, string ContentMd);

/// <summary>One page of a law's published rationale (gerekçe).</summary>
public sealed record LawClientsGerekceResponse(string? MevzuatId, string Text, int Page, int TotalPages);

/// <summary>One node of a piece of legislation's table of contents (kısım/bölüm/madde).</summary>
public sealed record LawClientsMaddeTreeNode(
    string? Title, string? MaddeNo, string? MaddeId, string? GerekceId,
    IReadOnlyList<LawClientsMaddeTreeNode> Children);

public sealed record LawClientsMaddeTreeResponse(int Count, IReadOnlyList<LawClientsMaddeTreeNode> Nodes);

/// <summary>
/// Full-text lookup of public legal documents, plus the two Mevzuat-specific reads (rationale,
/// article tree) large laws need. Consumed by the agent's <c>fetch_document</c>/
/// <c>get_mevzuat_gerekce</c>/<c>get_mevzuat_madde_tree</c> tools and directly by the UI's İlgili
/// Kararlar drawer / Karar Arama page (through this module's own Controller).
/// </summary>
public interface ILawClientsDocumentService
{
    /// <summary>
    /// Fetch one document's text (Markdown). <paramref name="articleId"/> narrows a Mevzuat fetch to
    /// a single article (a <c>maddeId</c> from <see cref="GetMaddeTreeAsync"/>) instead of the whole
    /// piece of legislation; other sources ignore it. Throws <see cref="Core.NotFoundException"/>
    /// when the source is unknown, cannot fetch, or has no such document.
    /// </summary>
    Task<LawClientsDocumentResponse> GetAsync(
        string source, string id, string? articleId = null, int? page = null, CancellationToken ct = default);

    /// <summary>Throws <see cref="Core.NotFoundException"/> when no rationale is published for the id.</summary>
    Task<LawClientsGerekceResponse> GetGerekceAsync(string gerekceId, int page = 1, int pageSize = 5000, CancellationToken ct = default);

    /// <summary>Empty when the legislation type has no published article tree (e.g. genelge/tebliğ).</summary>
    Task<LawClientsMaddeTreeResponse> GetMaddeTreeAsync(string mevzuatId, CancellationToken ct = default);
}

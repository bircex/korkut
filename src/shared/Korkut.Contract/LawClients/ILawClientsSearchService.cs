namespace Korkut.Contract.LawClients;

/// <summary>
/// One search against a named public Turkish legal database. Mirrors the agent's
/// <c>search_decisions</c> tool schema field-for-field; each source reads only the filters it
/// supports and ignores the rest.
/// </summary>
public sealed record LawClientsSearchRequest(
    string Query,
    int? Limit = null,
    int? Page = null,
    string? Sort = null,
    string? DateStart = null,
    string? DateEnd = null,
    string? Chamber = null,
    string? DecisionType = null,
    string? ApplicationType = null,
    string? Scope = null,
    string? LegislationType = null,
    string? SearchScope = null,
    bool? TamCumle = null,
    string? MevzuatNo = null,
    string? GazetteNumber = null);

/// <summary>One normalized search hit. <see cref="Id"/> is what <see cref="ILawClientsDocumentService.GetAsync"/> accepts back for the same <see cref="Source"/>.</summary>
public sealed record LawClientsSearchResult(
    string Source, string Id, string Title, string? Date, string? CaseNumber,
    string? DecisionNumber, string? Court, string? Chamber, string? Url, string? Snippet,
    string? GazetteNumber = null, string? GerekceId = null,
    string? DecisionType = null, string? FinalizationStatus = null, string? CourtType = null);

/// <summary><see cref="Total"/> is the database's reported match count (null when unknown); <see cref="Count"/> is how many hits were actually returned.</summary>
public sealed record LawClientsSearchResponse(long? Total, int Count, IReadOnlyList<LawClientsSearchResult> Results);

public sealed record LawClientsArticleMatch(string MaddeNo, int MatchCount, string Preview, string Content);

public sealed record LawClientsArticleSearchResponse(
    string MevzuatId, string Keyword, int TotalArticles, int MatchingArticles,
    IReadOnlyList<LawClientsArticleMatch> Results);

public sealed record LawClientsInsuranceDecisionMatch(string Header, int MatchCount, string Excerpt);

public sealed record LawClientsInsuranceSearchResponse(
    string IssueNumber, string Keyword, int TotalDecisions, int MatchingDecisions,
    IReadOnlyList<LawClientsInsuranceDecisionMatch> Results);

/// <summary>
/// Search across the public Turkish legal databases (Yargıtay, Danıştay, Emsal, KYB, Mevzuat,
/// Anayasa, GİB, KİK, Rekabet, Sayıştay, Uyuşmazlık, KVKK, BDDK, Sigorta Tahkim). Consumed both by
/// the agent's <c>search_decisions</c>/<c>search_within_*</c> tools and directly by the Karar Arama
/// UI (through this module's own Controller).
/// </summary>
public interface ILawClientsSearchService
{
    /// <summary>Search one named source. Throws <see cref="Core.BadRequestException"/> for an unknown source or a missing API key.</summary>
    Task<LawClientsSearchResponse> SearchAsync(string source, LawClientsSearchRequest request, CancellationToken ct = default);

    /// <summary>Article-level (MADDE) boolean keyword search within one piece of legislation's full text.</summary>
    Task<LawClientsArticleSearchResponse> SearchWithinMevzuatAsync(
        string mevzuatId, string keyword, bool caseSensitive, int maxResults, CancellationToken ct = default);

    /// <summary>Keyword search within one Sigorta Tahkim journal issue's arbitration decisions.</summary>
    Task<LawClientsInsuranceSearchResponse> SearchWithinSigortaTahkimAsync(
        string issueNumber, string keyword, int maxResults, CancellationToken ct = default);
}

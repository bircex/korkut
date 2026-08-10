namespace Korkut.LawClients.Services.Shared;

/// <summary>
/// One normalized hit from a public Turkish legal database, as the composite search tools return it.
/// Fields absent for a given source are left null. The <see cref="Id"/> is what <c>fetch_document</c>
/// passes back to that same <see cref="Source"/> to get the full text.
/// </summary>
internal sealed record SourceResult(
    string Source,
    string Id,
    string Title,
    string? Date = null,
    string? CaseNumber = null,
    string? DecisionNumber = null,
    string? Court = null,
    string? Chamber = null,
    string? Url = null,
    string? Snippet = null,
    string? GazetteNumber = null,
    string? GerekceId = null,
    // Bedesten-family sources (yargitay/danistay/emsal/kyb) only: the search API's own
    // kararTuru/kesinlesmeDurumu, and itemType.description (the court-system label — "Yargıtay
    // Kararı", "Yerel Hukuk Mahkemesi Kararı", "İstinaf Hukuk Mahkemesi Kararı" — which is what
    // distinguishes the two itemTypes merged under the single "emsal" source).
    string? DecisionType = null,
    string? FinalizationStatus = null,
    string? CourtType = null);

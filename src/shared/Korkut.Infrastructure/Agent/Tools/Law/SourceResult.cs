namespace Korkut.Infrastructure.Agent.Tools.Law;

/// <summary>
/// One normalized hit from a public Turkish legal database, as the composite search tools return it.
/// Fields absent for a given source are left null. The <see cref="Id"/> is what <c>fetch_document</c>
/// passes back to that same <see cref="Source"/> to get the full text.
/// </summary>
public sealed record SourceResult(
    string Source,
    string Id,
    string Title,
    string? Date = null,
    string? CaseNumber = null,
    string? DecisionNumber = null,
    string? Court = null,
    string? Chamber = null,
    string? Url = null,
    string? Snippet = null);

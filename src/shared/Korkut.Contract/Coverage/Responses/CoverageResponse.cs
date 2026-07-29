namespace Korkut.Contract.Coverage.Responses;

/// <summary>
/// What the assistant can draft: how many document types the catalog covers, the per-court
/// breakdown, and the legislation it is built on.
/// </summary>
public sealed record CoverageResponse(
    int TotalDecisions, IReadOnlyList<CoverageCourtResponse> ByCourt, IReadOnlyList<string> DecisionSources, string Legislation);

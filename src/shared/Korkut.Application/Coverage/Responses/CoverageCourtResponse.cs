namespace Korkut.Application.Coverage.Responses;

/// <summary>How many petition types the catalog covers for one court.</summary>
public sealed record CoverageCourtResponse(string Label, int Total);

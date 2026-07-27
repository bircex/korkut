namespace Korkut.Contract.Reports;

/// <summary>How many matters the office has, and how they are distributed across statuses.</summary>
public sealed record MatterStatsResponse(int Total, IReadOnlyDictionary<string, int> ByStatus);

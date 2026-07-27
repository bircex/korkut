namespace Korkut.Contract.Reports;

/// <summary>Correspondence volume, split by direction (incoming/outgoing).</summary>
public sealed record EvrakStatsResponse(int Total, IReadOnlyDictionary<string, int> ByDirection);

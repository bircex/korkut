namespace Korkut.Application.Reports.Responses;

/// <summary>Correspondence volume, split by direction (incoming/outgoing).</summary>
public sealed record EvrakStatsResponse(int Total, IReadOnlyDictionary<string, int> ByDirection);

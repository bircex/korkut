namespace Korkut.Application.Calendar.Responses;

/// <summary>The agenda plus the look-ahead window it was computed for.</summary>
public sealed record AgendaResponse(IReadOnlyList<AgendaItemResponse> Items, int Days);

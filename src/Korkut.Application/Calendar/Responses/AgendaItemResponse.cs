namespace Korkut.Application.Calendar.Responses;

/// <summary>An upcoming-agenda item (hearing or deadline) within the look-ahead window.</summary>
public sealed record AgendaItemResponse(
    string Kind, DateTimeOffset Date, Guid Id, Guid? MatterId, string Title, string? Status);

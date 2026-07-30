using Korkut.Contract.Clients;
using Korkut.Contract.Deadlines;
using Korkut.Contract.Hearings;

namespace Korkut.Contract.Matters;

/// <summary>A drafted document attached to a matter (agent-generated petition). Kept here so the
/// matter detail is self-describing; populated when the draft↔matter link is available.</summary>
public sealed record MatterDocumentResponse(
    Guid Id, Guid ConversationId, string Title, string Status, DateTimeOffset? UpdatedAt);

/// <summary>Matter detail: the matter brief plus its linked client, notes, hearings, deadlines,
/// and drafted documents. Mirrors the UI's MatterDetail (= MatterBrief + these children).</summary>
public sealed record MatterDetailResponse(
    Guid Id, string Title, string? ClientName, string? FileNo, string? Court, string? CaseType,
    string? Counterparty, string Status, DateTimeOffset? OpenedAt, DateTimeOffset? UpdatedAt,
    ClientResponse? Client, string? Notes,
    IReadOnlyList<HearingResponse> Hearings, IReadOnlyList<DeadlineResponse> Deadlines,
    IReadOnlyList<MatterDocumentResponse> Documents);

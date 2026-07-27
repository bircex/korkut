using Korkut.Contract.Hearings;

namespace Korkut.Contract.Matters;

/// <summary>Matter detail: the matter plus its hearings (duruşmalar), earliest first.</summary>
public sealed record MatterDetailResponse(
    Guid Id, string Title, Guid? ClientId, string Status, string? CourtNameSnapshot,
    DateTimeOffset CreatedAt, IReadOnlyList<HearingResponse> Hearings);

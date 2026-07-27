using Korkut.Contract.Hearings;

namespace Korkut.Hearings;

/// <summary>
/// Implements the cross-module <see cref="IHearingLookup"/> over the hearing repository, reading in
/// the caller's already-open tenant scope. Lets Matters build its detail without the Hearing type.
/// </summary>
public sealed class HearingLookup(IHearingRepository repository) : IHearingLookup
{
    public async Task<IReadOnlyList<HearingResponse>> ListForMatterAsync(
        Guid officeId, Guid matterId, CancellationToken ct = default)
        => (await repository.ListForMatterAsync(officeId, matterId, ct)).Select(HearingMapping.ToResponse).ToList();
}

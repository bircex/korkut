using Korkut.Matters.Repositories;
using Korkut.Matters.Entities;
using Korkut.Contract;
using Korkut.Contract.Courts;
using Korkut.Contract.Hearings;
using Korkut.Contract.Matters;
using Korkut.Core;

namespace Korkut.Matters.Services;

/// <summary>
/// Matter use-cases over the repository port; every call runs in the caller's tenant scope and
/// RETURNS a <see cref="Result"/>. Owns the court-title snapshot rule. Cross-module reads (court
/// name, a matter's hearings) go through Contract lookups, never other modules' entities.
/// </summary>
internal sealed class MatterService(
    IMatterRepository matters, IHearingLookup hearings, ICourtLookup courts,
    IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IMatterService
{
    public Task<Result<IReadOnlyList<MatterResponse>>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<IReadOnlyList<MatterResponse>>.Ok((await matters.ListAsync(office, ct)).Select(ToResponse).ToList()), ct);

    public Task<Result<MatterDetailResponse>> GetAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var matter = await matters.FindAsync(office, id, ct);
            if (matter is null)
            {
                return Result<MatterDetailResponse>.NotFound("Dosya bulunamadı.");
            }

            var hearingRows = await hearings.ListForMatterAsync(office, id, ct);
            return Result<MatterDetailResponse>.Ok(new MatterDetailResponse(
                matter.Id, matter.Title, matter.ClientId, matter.Status, matter.CourtNameSnapshot,
                matter.CreatedAt, hearingRows));
        }, ct);

    public Task<Result<MatterResponse>> CreateAsync(CreateMatterRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            // Snapshot the court's CURRENT title onto the matter; a later rename never rewrites it.
            var courtName = request.CourtId is { } courtId
                ? await courts.GetNameAsync(office, courtId, ct)
                : null;

            var matter = new Matter(office, request.Title!.Trim(), request.ClientId, request.CourtId, courtName, clock.UtcNow);
            await matters.AddAsync(matter, ct);
            return Result<MatterResponse>.Ok(ToResponse(matter));
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            await matters.SoftDeleteAsync(office, id, clock.UtcNow, ct);
            return Result.Ok();
        }, ct);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static MatterResponse ToResponse(Matter m) =>
        new(m.Id, m.Title, m.ClientId, m.Status, m.CourtNameSnapshot, m.CreatedAt);
}

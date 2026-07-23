using Korkut.Application.Common;
using Korkut.Application.Courts;
using Korkut.Application.Errors;
using Korkut.Application.Hearings;
using Korkut.Application.Matters.Requests;
using Korkut.Application.Matters.Responses;
using Korkut.Domain.Matters;
using Korkut.Domain.Time;

namespace Korkut.Application.Matters;

/// <summary>Matter use-cases over the repository ports; every call runs in the caller's tenant scope.</summary>
public sealed class MatterService(
    IMatterRepository matters, IHearingRepository hearings, ICourtRepository courts,
    IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IMatterService
{
    public Task<IReadOnlyList<MatterResponse>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            (IReadOnlyList<MatterResponse>)(await matters.ListAsync(office, ct)).Select(ToResponse).ToList(), ct);

    public Task<MatterDetailResponse> GetAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var matter = await matters.FindAsync(office, id, ct) ?? throw new NotFoundException("Dosya bulunamadı.");
            var list = await hearings.ListForMatterAsync(office, id, ct);
            return new MatterDetailResponse(
                matter.Id, matter.Title, matter.ClientId, matter.Status, matter.CourtNameSnapshot,
                matter.CreatedAt, list.Select(HearingService.ToResponse).ToList());
        }, ct);

    public Task<MatterResponse> CreateAsync(CreateMatterRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            // Snapshot the court's CURRENT title onto the matter; a later rename never rewrites it.
            var courtName = request.CourtId is { } courtId
                ? (await courts.FindAsync(office, courtId, ct))?.Name
                : null;

            var matter = new Matter(office, request.Title!.Trim(), request.ClientId, request.CourtId, courtName, clock.UtcNow);
            await matters.AddAsync(matter, ct);
            return ToResponse(matter);
        }, ct);

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(office => matters.SoftDeleteAsync(office, id, clock.UtcNow, ct), ct);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static MatterResponse ToResponse(Matter m) =>
        new(m.Id, m.Title, m.ClientId, m.Status, m.CourtNameSnapshot, m.CreatedAt);
}

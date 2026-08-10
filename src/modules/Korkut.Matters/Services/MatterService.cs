using Korkut.Matters.Repositories;
using Korkut.Matters.Entities;
using Korkut.Contract;
using Korkut.Contract.Clients;
using Korkut.Contract.Courts;
using Korkut.Contract.Deadlines;
using Korkut.Contract.Hearings;
using Korkut.Contract.Matters;
using Korkut.Core;

namespace Korkut.Matters.Services;

/// <summary>
/// Matter use-cases over the repository port; every call runs in the caller's tenant scope and
/// RETURNS a <see cref="Result"/>. Owns the court-title snapshot rule. Cross-module reads (client
/// brief, court name, a matter's hearings + deadlines) go through Contract lookups, never other
/// modules' entities.
/// </summary>
internal sealed class MatterService(
    IMatterRepository matters, IHearingLookup hearings, IDeadlineLookup deadlines, ICourtLookup courts,
    IClientLookup clients, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IMatterService
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
            var deadlineRows = await deadlines.ListInRangeAsync(office, null, null, id, ct);
            var client = matter.ClientId is { } clientId ? await clients.BriefAsync(office, clientId, ct) : null;
            return Result<MatterDetailResponse>.Ok(new MatterDetailResponse(
                matter.Id, matter.Title, matter.ClientNameSnapshot, matter.FileNo, matter.CourtNameSnapshot,
                matter.CaseType, matter.Counterparty, matter.Status, matter.OpenedAt, matter.UpdatedAt,
                client, matter.Notes, hearingRows, deadlineRows, Documents: []));
        }, ct);

    public Task<Result<MatterResponse>> CreateAsync(CreateMatterRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            // The client is bound by id and its name snapshotted at link time: a later client
            // rename never rewrites the matter's historical text.
            string? clientName = null;
            if (request.ClientId is { } clientId)
            {
                clientName = await clients.GetNameAsync(office, clientId, ct);
                if (clientName is null)
                {
                    return Result<MatterResponse>.NotFound("Müvekkil bulunamadı.");
                }
            }

            // The court title is snapshotted: use the free-text value if given, else the linked
            // court's CURRENT title. A later court rename never rewrites the matter's text.
            var courtName = string.IsNullOrWhiteSpace(request.Court)
                ? request.CourtId is { } courtId ? await courts.GetNameAsync(office, courtId, ct) : null
                : request.Court.Trim();

            var matter = new Matter(
                office, request.Title!.Trim(), request.ClientId, clientName,
                request.FileNo?.Trim(), request.CaseType?.Trim(), request.Counterparty?.Trim(),
                request.CourtId, courtName, clock.UtcNow);
            await matters.AddAsync(matter, ct);
            return Result<MatterResponse>.Ok(ToResponse(matter));
        }, ct);

    public Task<Result<MatterResponse>> UpdateAsync(Guid id, UpdateMatterRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var matter = await matters.FindForUpdateAsync(office, id, ct);
            if (matter is null)
            {
                return Result<MatterResponse>.NotFound("Dosya bulunamadı.");
            }

            string? clientName = null;
            if (request.ClientId is { } clientId)
            {
                clientName = await clients.GetNameAsync(office, clientId, ct);
                if (clientName is null)
                {
                    return Result<MatterResponse>.NotFound("Müvekkil bulunamadı.");
                }
            }

            matter.Update(request.Title?.Trim(), request.ClientId, clientName, request.FileNo?.Trim(),
                request.CaseType?.Trim(), request.Counterparty?.Trim(), request.Status, request.Notes?.Trim(), clock.UtcNow);
            await matters.SaveChangesAsync(ct);
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
        new(m.Id, m.Title, m.ClientNameSnapshot, m.FileNo, m.CourtNameSnapshot, m.CaseType,
            m.Counterparty, m.Status, m.OpenedAt, m.UpdatedAt);
}

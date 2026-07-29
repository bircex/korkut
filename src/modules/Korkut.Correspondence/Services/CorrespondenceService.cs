using Korkut.Correspondence.Repositories;
using Korkut.Correspondence.Entities;
using Korkut.Contract;
using Korkut.Contract.Correspondence;
using Korkut.Core;

namespace Korkut.Correspondence.Services;

/// <summary>Correspondence use-cases over the repository port; every call RETURNS a <see cref="Result"/>.</summary>
internal sealed class CorrespondenceService(
    ICorrespondenceRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock)
    : ICorrespondenceService
{
    public Task<Result<IReadOnlyList<CorrespondenceResponse>>> ListAsync(string? direction, CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<IReadOnlyList<CorrespondenceResponse>>.Ok((await repository.ListAsync(office, direction, ct)).Select(ToResponse).ToList()), ct);

    public Task<Result<CorrespondenceResponse>> CreateAsync(CreateCorrespondenceRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var item = new CorrespondenceItem(
                office, request.Direction!, request.Title!.Trim(), request.ReferenceNo,
                request.DocDate, request.MatterId, request.Notes, clock.UtcNow);
            await repository.AddAsync(item, ct);
            return Result<CorrespondenceResponse>.Ok(ToResponse(item));
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
            await repository.RemoveAsync(office, id, ct)
                ? Result.Ok()
                : Result.NotFound("Evrak kaydı bulunamadı."), ct);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static CorrespondenceResponse ToResponse(CorrespondenceItem c) =>
        new(c.Id, c.MatterId, c.Direction, c.Title, c.ReferenceNo, c.DocDate, c.Notes, c.CreatedAt);
}

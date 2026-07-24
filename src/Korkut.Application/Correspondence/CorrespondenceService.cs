using Korkut.Core;
using Korkut.Application.Correspondence.Requests;
using Korkut.Application.Correspondence.Responses;
using Korkut.Application.Errors;
using Korkut.Domain.Correspondence;

namespace Korkut.Application.Correspondence;

/// <summary>Correspondence use-cases over the repository port.</summary>
public sealed class CorrespondenceService(
    ICorrespondenceRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock)
    : ICorrespondenceService
{
    public Task<IReadOnlyList<CorrespondenceResponse>> ListAsync(string? direction, CancellationToken ct = default)
        => InScopeAsync(async office =>
            (IReadOnlyList<CorrespondenceResponse>)(await repository.ListAsync(office, direction, ct)).Select(ToResponse).ToList(), ct);

    public Task<CorrespondenceResponse> CreateAsync(CreateCorrespondenceRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var item = new CorrespondenceItem(
                office, request.Direction!, request.Title!.Trim(), request.ReferenceNo,
                request.DocDate, request.MatterId, request.Notes, clock.UtcNow);
            await repository.AddAsync(item, ct);
            return ToResponse(item);
        }, ct);

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            if (!await repository.RemoveAsync(office, id, ct))
            {
                throw new NotFoundException("Evrak kaydı bulunamadı.");
            }

            return true;
        }, ct);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static CorrespondenceResponse ToResponse(CorrespondenceItem c) =>
        new(c.Id, c.MatterId, c.Direction, c.Title, c.ReferenceNo, c.DocDate, c.Notes, c.CreatedAt);
}

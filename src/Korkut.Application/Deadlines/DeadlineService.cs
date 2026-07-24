using Korkut.Core;
using Korkut.Application.Deadlines.Requests;
using Korkut.Application.Deadlines.Responses;
using Korkut.Application.Errors;
using Korkut.Domain.Deadlines;

namespace Korkut.Application.Deadlines;

/// <summary>Deadline use-cases over the repository port; completion is idempotent-by-refusal.</summary>
public sealed class DeadlineService(
    IDeadlineRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IDeadlineService
{
    public Task<IReadOnlyList<DeadlineResponse>> ListAsync(Guid? matterId, CancellationToken ct = default)
        => InScopeAsync(async office =>
            (IReadOnlyList<DeadlineResponse>)(await repository.ListAsync(office, matterId, ct)).Select(ToResponse).ToList(), ct);

    public Task<DeadlineResponse> CreateAsync(CreateDeadlineRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var deadline = new Deadline(
                office, request.Title!.Trim(), request.DueDate, request.Kind, request.MatterId,
                request.TimeZoneId, request.ReminderDaysBefore, clock.UtcNow);
            await repository.AddAsync(deadline, ct);
            return ToResponse(deadline);
        }, ct);

    public Task CompleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var deadline = await repository.FindForUpdateAsync(office, id, ct);
            if (deadline is null || !deadline.Complete(clock.UtcNow))
            {
                throw new NotFoundException("Süre bulunamadı veya zaten tamamlandı.");
            }

            await repository.SaveChangesAsync(ct);
            return true;
        }, ct);

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            if (!await repository.RemoveAsync(office, id, ct))
            {
                throw new NotFoundException("Süre bulunamadı.");
            }

            return true;
        }, ct);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static DeadlineResponse ToResponse(Deadline d) =>
        new(d.Id, d.Title, d.DueDate, d.Kind, d.MatterId, d.TimeZoneId, d.ReminderDaysBefore, d.CompletedAt, d.CreatedAt);
}

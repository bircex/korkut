using Korkut.Deadlines.Entities;
using Korkut.Contract;
using Korkut.Contract.Deadlines;
using Korkut.Core;

namespace Korkut.Deadlines;

/// <summary>
/// Deadline use-cases over the repository port; completion is idempotent-by-refusal. Every call
/// runs in the caller's tenant scope and RETURNS a <see cref="Result"/>.
/// </summary>
internal sealed class DeadlineService(
    IDeadlineRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IDeadlineService
{
    public Task<Result<IReadOnlyList<DeadlineResponse>>> ListAsync(Guid? matterId, CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<IReadOnlyList<DeadlineResponse>>.Ok((await repository.ListAsync(office, matterId, ct)).Select(ToResponse).ToList()), ct);

    public Task<Result<DeadlineResponse>> CreateAsync(CreateDeadlineRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var deadline = new Deadline(
                office, request.Title!.Trim(), request.DueDate, request.Kind, request.MatterId,
                request.TimeZoneId, request.ReminderDaysBefore, clock.UtcNow);
            await repository.AddAsync(deadline, ct);
            return Result<DeadlineResponse>.Ok(ToResponse(deadline));
        }, ct);

    public Task<Result> CompleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var deadline = await repository.FindForUpdateAsync(office, id, ct);
            if (deadline is null || !deadline.Complete(clock.UtcNow))
            {
                return Result.NotFound("Süre bulunamadı veya zaten tamamlandı.");
            }

            await repository.SaveChangesAsync(ct);
            return Result.Ok();
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
            await repository.RemoveAsync(office, id, ct)
                ? Result.Ok()
                : Result.NotFound("Süre bulunamadı."), ct);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static DeadlineResponse ToResponse(Deadline d) =>
        new(d.Id, d.Title, d.DueDate, d.Kind, d.MatterId, d.TimeZoneId, d.ReminderDaysBefore, d.CompletedAt, d.CreatedAt);
}

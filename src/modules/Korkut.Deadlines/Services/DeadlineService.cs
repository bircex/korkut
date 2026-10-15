using Korkut.Deadlines.Repositories;
using Korkut.Deadlines.Entities;
using Korkut.Contract;
using Korkut.Contract.Deadlines;
using Korkut.Contract.Matters;
using Korkut.Core;

namespace Korkut.Deadlines.Services;

/// <summary>
/// Deadline use-cases over the repository port; completion is idempotent-by-refusal. Every call
/// runs in the caller's tenant scope and RETURNS a <see cref="Result"/>. A deadline is visible when
/// its matter is (or, with no matter, to whoever added it); any other reads as not found.
/// </summary>
internal sealed class DeadlineService(
    IDeadlineRepository repository, IMatterAccess access, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IDeadlineService
{
    public Task<Result<IReadOnlyList<DeadlineResponse>>> ListAsync(Guid? matterId, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var scope = await access.ScopeAsync(office, ct);
            return Result<IReadOnlyList<DeadlineResponse>>.Ok((await repository.ListAsync(office, matterId, ct))
                .Where(d => scope.CanSee(d.MatterId, d.CreatedBy)).Select(ToResponse).ToList());
        }, ct);

    public Task<Result<DeadlineResponse>> CreateAsync(CreateDeadlineRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            if (request.MatterId is { } matterId && !(await access.ScopeAsync(office, ct)).CanSee(matterId))
            {
                return Result<DeadlineResponse>.NotFound("Dosya bulunamadı.");
            }

            var deadline = new Deadline(
                office, request.Title!.Trim(), request.DueDate, request.Type ?? "internal", request.MatterId,
                request.TimeZoneId, request.ReminderDaysBefore,
                string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(), clock.UtcNow, currentUser.UserId);
            await repository.AddAsync(deadline, ct);
            return Result<DeadlineResponse>.Ok(ToResponse(deadline));
        }, ct);

    public Task<Result> CompleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var deadline = await repository.FindForUpdateAsync(office, id, ct);
            if (deadline is null || !(await access.ScopeAsync(office, ct)).CanSee(deadline.MatterId, deadline.CreatedBy)
                || !deadline.Complete(clock.UtcNow))
            {
                return Result.NotFound("Süre bulunamadı veya zaten tamamlandı.");
            }

            await repository.SaveChangesAsync(ct);
            return Result.Ok();
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var deadline = await repository.FindAsync(office, id, ct);
            if (deadline is null || !(await access.ScopeAsync(office, ct)).CanSee(deadline.MatterId, deadline.CreatedBy))
            {
                return Result.NotFound("Süre bulunamadı.");
            }

            return await repository.RemoveAsync(office, id, ct) ? Result.Ok() : Result.NotFound("Süre bulunamadı.");
        }, ct);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static DeadlineResponse ToResponse(Deadline d) =>
        new(d.Id, d.Title, d.DueDate, d.Kind, d.CompletedAt is null ? "open" : "done", d.MatterId, d.Notes,
            d.TimeZoneId, d.ReminderDaysBefore, d.CompletedAt, d.CreatedAt, d.CreatedBy);
}

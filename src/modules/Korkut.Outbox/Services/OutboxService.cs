using Korkut.Outbox.Entities;
using Korkut.Contract;
using Korkut.Contract.Outbox;
using Korkut.Core;

namespace Korkut.Outbox;

/// <summary>
/// Outbox review over the repository port. The lifecycle rules live on the aggregate; this only
/// decides that a refused transition reads as NotFound to the caller. Every call RETURNS a Result.
/// </summary>
internal sealed class OutboxService(
    IOutboxRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser) : IOutboxService
{
    public Task<Result<IReadOnlyList<OutboxMessageResponse>>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<IReadOnlyList<OutboxMessageResponse>>.Ok((await repository.ListAsync(office, ct)).Select(ToResponse).ToList()), ct);

    public Task<Result<OutboxMessageResponse>> GetAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var message = await repository.FindAsync(office, id, ct);
            return message is null ? NotFound() : Result<OutboxMessageResponse>.Ok(ToResponse(message));
        }, ct);

    public Task<Result<OutboxMessageResponse>> EditDraftAsync(Guid id, EditOutboxMessageRequest request, CancellationToken ct = default)
        => TransitionAsync(id, m => m.EditDraft(request.ToAddr!.Trim(), request.Subject, request.Body!, request.SendAt), ct);

    public Task<Result<OutboxMessageResponse>> ConfirmAsync(Guid id, CancellationToken ct = default)
        => TransitionAsync(id, m => m.Confirm(), ct);

    public Task<Result<OutboxMessageResponse>> CancelAsync(Guid id, CancellationToken ct = default)
        => TransitionAsync(id, m => m.Cancel(), ct);

    private Task<Result<OutboxMessageResponse>> TransitionAsync(
        Guid id, Func<OutboundMessage, bool> transition, CancellationToken ct)
        => InScopeAsync(async office =>
        {
            var message = await repository.FindForUpdateAsync(office, id, ct);
            if (message is null || !transition(message))
            {
                return NotFound();
            }

            await repository.SaveChangesAsync(ct);
            return Result<OutboxMessageResponse>.Ok(ToResponse(message));
        }, ct);

    private static Result<OutboxMessageResponse> NotFound() => Result<OutboxMessageResponse>.NotFound("Mesaj bulunamadı.");

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static OutboxMessageResponse ToResponse(OutboundMessage m) =>
        new(m.Id, m.Kind, m.ToAddr, m.Subject, m.Body, m.SendAt, m.Status, m.Error, m.CreatedAt, m.SentAt);
}

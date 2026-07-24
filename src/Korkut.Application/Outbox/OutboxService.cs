using Korkut.Core;
using Korkut.Application.Errors;
using Korkut.Application.Outbox.Requests;
using Korkut.Application.Outbox.Responses;
using Korkut.Domain.Outbox;

namespace Korkut.Application.Outbox;

/// <summary>
/// Outbox review over the repository port. The lifecycle rules live on the aggregate; this
/// only decides that a refused transition reads as "not found" to the caller.
/// </summary>
public sealed class OutboxService(
    IOutboxRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser) : IOutboxService
{
    public Task<IReadOnlyList<OutboxMessageResponse>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            (IReadOnlyList<OutboxMessageResponse>)(await repository.ListAsync(office, ct)).Select(ToResponse).ToList(), ct);

    public Task<OutboxMessageResponse> GetAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
            ToResponse(await repository.FindAsync(office, id, ct) ?? throw NotFound()), ct);

    public Task<OutboxMessageResponse> EditDraftAsync(Guid id, EditOutboxMessageRequest request, CancellationToken ct = default)
        => TransitionAsync(id, m => m.EditDraft(request.ToAddr!.Trim(), request.Subject, request.Body!, request.SendAt), ct);

    public Task<OutboxMessageResponse> ConfirmAsync(Guid id, CancellationToken ct = default)
        => TransitionAsync(id, m => m.Confirm(), ct);

    public Task<OutboxMessageResponse> CancelAsync(Guid id, CancellationToken ct = default)
        => TransitionAsync(id, m => m.Cancel(), ct);

    private Task<OutboxMessageResponse> TransitionAsync(Guid id, Func<OutboundMessage, bool> transition, CancellationToken ct)
        => InScopeAsync(async office =>
        {
            var message = await repository.FindForUpdateAsync(office, id, ct);
            if (message is null || !transition(message))
            {
                throw NotFound();
            }

            await repository.SaveChangesAsync(ct);
            return ToResponse(message);
        }, ct);

    private static NotFoundException NotFound() => new("Mesaj bulunamadı.");

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static OutboxMessageResponse ToResponse(OutboundMessage m) =>
        new(m.Id, m.Kind, m.ToAddr, m.Subject, m.Body, m.SendAt, m.Status, m.Error, m.CreatedAt, m.SentAt);
}

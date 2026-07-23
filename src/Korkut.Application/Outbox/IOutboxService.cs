using Korkut.Application.Outbox.Requests;
using Korkut.Application.Outbox.Responses;

namespace Korkut.Application.Outbox;

/// <summary>
/// Outbound-message review use-cases: a human reviews the assistant's drafts, then confirms
/// or cancels. A message that cannot make the requested transition raises
/// <c>NotFoundException</c>, matching the existing contract.
/// </summary>
public interface IOutboxService
{
    Task<IReadOnlyList<OutboxMessageResponse>> ListAsync(CancellationToken ct = default);

    Task<OutboxMessageResponse> GetAsync(Guid id, CancellationToken ct = default);

    Task<OutboxMessageResponse> EditDraftAsync(Guid id, EditOutboxMessageRequest request, CancellationToken ct = default);

    Task<OutboxMessageResponse> ConfirmAsync(Guid id, CancellationToken ct = default);

    Task<OutboxMessageResponse> CancelAsync(Guid id, CancellationToken ct = default);
}

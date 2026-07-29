namespace Korkut.Contract.Outbox;

/// <summary>
/// Outbound-message review use-cases (cross-module contract): a human reviews the assistant's
/// drafts, then confirms or cancels. A message that cannot make the requested transition is
/// NotFound. Every call RETURNS a <see cref="Result"/>.
/// </summary>
public interface IOutboxService
{
    Task<Result<IReadOnlyList<OutboxMessageResponse>>> ListAsync(CancellationToken ct = default);

    Task<Result<OutboxMessageResponse>> GetAsync(Guid id, CancellationToken ct = default);

    Task<Result<OutboxMessageResponse>> EditDraftAsync(Guid id, EditOutboxMessageRequest request, CancellationToken ct = default);

    Task<Result<OutboxMessageResponse>> ConfirmAsync(Guid id, CancellationToken ct = default);

    Task<Result<OutboxMessageResponse>> CancelAsync(Guid id, CancellationToken ct = default);
}

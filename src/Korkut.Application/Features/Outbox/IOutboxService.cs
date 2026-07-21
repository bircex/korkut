using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.Outbox;
using Korkut.SharedKernel.Time;

namespace Korkut.Application.Features.Outbox;

/// <summary>
/// Outbound-message review-queue use-cases: a human reviews the assistant's drafts
/// (list/get), edits a draft, confirms (draft → queued) or cancels. The draft-creation
/// timestamp is supplied from the clock here.
/// </summary>
public interface IOutboxService
{
    Task<IReadOnlyList<OutboundMessage>> ListAsync(Guid officeId, CancellationToken ct = default);

    Task<OutboundMessage?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<Guid> CreateDraftAsync(Guid officeId, string kind, string toAddr, string subject, string body, DateTimeOffset? sendAt, CancellationToken ct = default);

    /// <summary>Edit a draft and return the refreshed message, or null if missing/not a draft.</summary>
    Task<OutboundMessage?> EditDraftAsync(Guid officeId, Guid id, string toAddr, string subject, string body, DateTimeOffset? sendAt, CancellationToken ct = default);

    /// <summary>Confirm a draft and return the refreshed message, or null if missing/not a draft.</summary>
    Task<OutboundMessage?> ConfirmAsync(Guid officeId, Guid id, CancellationToken ct = default);

    /// <summary>Cancel a message and return the refreshed message, or null if missing/already sent.</summary>
    Task<OutboundMessage?> CancelAsync(Guid officeId, Guid id, CancellationToken ct = default);
}

/// <summary>Outbox review orchestration over the repository port.</summary>
public sealed class OutboxService(IOutboxRepository repo, IClock clock) : IOutboxService
{
    public Task<IReadOnlyList<OutboundMessage>> ListAsync(Guid officeId, CancellationToken ct = default)
        => repo.ListAsync(officeId, ct);

    public Task<OutboundMessage?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => repo.GetAsync(officeId, id, ct);

    public Task<Guid> CreateDraftAsync(Guid officeId, string kind, string toAddr, string subject, string body, DateTimeOffset? sendAt, CancellationToken ct = default)
        => repo.CreateDraftAsync(officeId, kind, toAddr, subject, body, sendAt, clock.UtcNow, ct);

    public async Task<OutboundMessage?> EditDraftAsync(Guid officeId, Guid id, string toAddr, string subject, string body, DateTimeOffset? sendAt, CancellationToken ct = default)
        => await repo.EditDraftAsync(officeId, id, toAddr, subject, body, sendAt, ct) ? await repo.GetAsync(officeId, id, ct) : null;

    public async Task<OutboundMessage?> ConfirmAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await repo.ConfirmAsync(officeId, id, ct) ? await repo.GetAsync(officeId, id, ct) : null;

    public async Task<OutboundMessage?> CancelAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await repo.CancelAsync(officeId, id, ct) ? await repo.GetAsync(officeId, id, ct) : null;
}

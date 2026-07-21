using Korkut.Domain.Outbox;

namespace Korkut.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for the outbound-message review queue. Distinct from the jobs-side
/// <c>IOutboxStore</c> (dispatch/lease/send): this is the human review surface (list/get/
/// edit-draft/confirm/cancel). Tenant context is set by the caller (RLS).
/// </summary>
public interface IOutboxRepository
{
    Task<IReadOnlyList<OutboundMessage>> ListAsync(Guid officeId, CancellationToken ct = default);

    Task<OutboundMessage?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<Guid> CreateDraftAsync(Guid officeId, string kind, string toAddr, string subject, string body, DateTimeOffset? sendAt, DateTimeOffset createdAt, CancellationToken ct = default);

    /// <summary>Edit a DRAFT's recipient/subject/body/schedule. False if missing or not a draft.</summary>
    Task<bool> EditDraftAsync(Guid officeId, Guid id, string toAddr, string subject, string body, DateTimeOffset? sendAt, CancellationToken ct = default);

    /// <summary>Confirm a DRAFT → queued. False if missing or not a draft.</summary>
    Task<bool> ConfirmAsync(Guid officeId, Guid id, CancellationToken ct = default);

    /// <summary>Cancel a not-yet-sent message. False if missing or already sent.</summary>
    Task<bool> CancelAsync(Guid officeId, Guid id, CancellationToken ct = default);
}

using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.Outbox;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF/Postgres adapter for <see cref="IOutboxRepository"/> — the human review surface over
/// <c>outbound_messages</c>. Only DRAFT rows may be edited/confirmed; confirming moves
/// draft → queued (a background sender later transitions queued → sent/failed). Tenant
/// context is set by the caller (RLS).
/// </summary>
public sealed class OutboxRepository(KorkutDbContext db) : IOutboxRepository
{
    public async Task<IReadOnlyList<OutboundMessage>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await db.OutboundMessages.AsNoTracking().Where(x => x.OfficeId == officeId)
            .OrderByDescending(x => x.CreatedAt).Select(Projection).ToListAsync(ct);

    public Task<OutboundMessage?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => db.OutboundMessages.AsNoTracking().Where(x => x.OfficeId == officeId && x.Id == id)
            .Select(Projection).SingleOrDefaultAsync(ct);

    public async Task<Guid> CreateDraftAsync(
        Guid officeId, string kind, string toAddr, string subject, string body, DateTimeOffset? sendAt, DateTimeOffset createdAt, CancellationToken ct = default)
    {
        var msg = new OutboundMessageEntity
        {
            Id = UuidV7.NewId(), OfficeId = officeId, Kind = kind, ToAddr = toAddr, Subject = subject,
            Body = body, SendAt = sendAt, Status = "draft", CreatedAt = createdAt,
        };
        db.OutboundMessages.Add(msg);
        await db.SaveChangesAsync(ct);
        return msg.Id;
    }

    public async Task<bool> EditDraftAsync(
        Guid officeId, Guid id, string toAddr, string subject, string body, DateTimeOffset? sendAt, CancellationToken ct = default)
        => await db.OutboundMessages.Where(x => x.OfficeId == officeId && x.Id == id && x.Status == "draft")
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.ToAddr, toAddr)
                .SetProperty(x => x.Subject, subject)
                .SetProperty(x => x.Body, body)
                .SetProperty(x => x.SendAt, sendAt), ct) > 0;

    public async Task<bool> ConfirmAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await db.OutboundMessages.Where(x => x.OfficeId == officeId && x.Id == id && x.Status == "draft")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "queued"), ct) > 0;

    public async Task<bool> CancelAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await db.OutboundMessages.Where(x => x.OfficeId == officeId && x.Id == id && x.Status != "sent")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "cancelled"), ct) > 0;

    private static readonly System.Linq.Expressions.Expression<Func<OutboundMessageEntity, OutboundMessage>> Projection =
        m => new OutboundMessage(m.Id, m.Kind, m.ToAddr, m.Subject, m.Body, m.SendAt, m.Status, m.Error, m.CreatedAt, m.SentAt);
}

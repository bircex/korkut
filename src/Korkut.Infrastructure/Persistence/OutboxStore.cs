using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Korkut.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>
/// The outbound-message review queue: drafts an assistant produces, edited/confirmed/
/// cancelled by a human before sending. Tenant context is set by the caller (RLS).
/// Only DRAFT messages may be edited or confirmed; confirming moves draft → queued
/// (a background sender, out of scope here, later transitions queued → sent/failed).
/// </summary>
public sealed class OutboxStore(KorkutDbContext db, IClock clock)
{
    public Task<List<OutboundMessageEntity>> ListAsync(Guid officeId, CancellationToken ct = default)
        => db.OutboundMessages.AsNoTracking().Where(x => x.OfficeId == officeId)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct);

    public Task<OutboundMessageEntity?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => db.OutboundMessages.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == id, ct);

    /// <summary>Create a draft (used by the assistant / seeding).</summary>
    public async Task<Guid> CreateDraftAsync(
        Guid officeId, string kind, string toAddr, string subject, string body, DateTimeOffset? sendAt, CancellationToken ct = default)
    {
        var msg = new OutboundMessageEntity
        {
            Id = UuidV7.NewId(), OfficeId = officeId, Kind = kind, ToAddr = toAddr, Subject = subject,
            Body = body, SendAt = sendAt, Status = "draft", CreatedAt = clock.UtcNow,
        };
        db.OutboundMessages.Add(msg);
        await db.SaveChangesAsync(ct);
        return msg.Id;
    }

    /// <summary>Edit a DRAFT's recipient/subject/body/schedule. Returns false if missing or not a draft.</summary>
    public async Task<bool> EditDraftAsync(
        Guid officeId, Guid id, string toAddr, string subject, string body, DateTimeOffset? sendAt, CancellationToken ct = default)
        => await db.OutboundMessages.Where(x => x.OfficeId == officeId && x.Id == id && x.Status == "draft")
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.ToAddr, toAddr)
                .SetProperty(x => x.Subject, subject)
                .SetProperty(x => x.Body, body)
                .SetProperty(x => x.SendAt, sendAt), ct) > 0;

    /// <summary>Confirm a DRAFT → queued. Returns false if missing or not a draft.</summary>
    public async Task<bool> ConfirmAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await db.OutboundMessages.Where(x => x.OfficeId == officeId && x.Id == id && x.Status == "draft")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "queued"), ct) > 0;

    /// <summary>Cancel a message that has not yet been sent. Returns false if missing or already sent.</summary>
    public async Task<bool> CancelAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await db.OutboundMessages.Where(x => x.OfficeId == officeId && x.Id == id && x.Status != "sent")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "cancelled"), ct) > 0;
}

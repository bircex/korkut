using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Korkut.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>A conversation summary with its author resolved to a display value.</summary>
public sealed record SessionBrief(Guid Id, string Title, Guid? MatterId, string? CreatedByName, DateTimeOffset? UpdatedAt);

/// <summary>
/// Assistant conversation ("session") persistence: CRUD, message append/list, and clone.
/// Tenant context is set by the caller (RLS). Recency is tracked by conversations.updated_at,
/// bumped whenever a message is appended.
/// </summary>
public sealed class SessionStore(KorkutDbContext db, IClock clock)
{
    public async Task<Guid> CreateAsync(Guid officeId, Guid createdBy, Guid? matterId, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var conv = new ConversationEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            Title = "Yeni görüşme",
            MatterId = matterId,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Conversations.Add(conv);
        await db.SaveChangesAsync(ct);
        return conv.Id;
    }

    public Task<ConversationEntity?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => db.Conversations.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == id, ct);

    public async Task<(IReadOnlyList<SessionBrief> Items, int Total)> ListAsync(Guid officeId, int limit, int offset, CancellationToken ct = default)
    {
        var baseQuery = db.Conversations.AsNoTracking().Where(x => x.OfficeId == officeId);
        var total = await baseQuery.CountAsync(ct);
        var page = await baseQuery.OrderByDescending(x => x.UpdatedAt).Skip(offset).Take(limit).ToListAsync(ct);
        var items = await ToBriefsAsync(officeId, page, ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<ChatMessageEntity>> MessagesAsync(Guid officeId, Guid conversationId, CancellationToken ct = default)
        => await db.ChatMessages.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.ConversationId == conversationId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task AppendMessageAsync(Guid officeId, Guid conversationId, string role, string content, CancellationToken ct = default)
    {
        db.ChatMessages.Add(new ChatMessageEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            ConversationId = conversationId,
            Role = role,
            Content = content,
            CreatedAt = clock.UtcNow,
        });
        await db.Conversations.Where(x => x.OfficeId == officeId && x.Id == conversationId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, clock.UtcNow), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> RenameAsync(Guid officeId, Guid id, string title, CancellationToken ct = default)
        => await db.Conversations.Where(x => x.OfficeId == officeId && x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Title, title).SetProperty(x => x.UpdatedAt, clock.UtcNow), ct) > 0;

    /// <summary>Set the title only if it is still the default placeholder (first-message auto-title).</summary>
    public async Task<bool> SetTitleIfDefaultAsync(Guid officeId, Guid id, string title, CancellationToken ct = default)
        => await db.Conversations.Where(x => x.OfficeId == officeId && x.Id == id && x.Title == "Yeni görüşme")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Title, title), ct) > 0;

    public async Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default)
    {
        await db.ChatMessages.Where(x => x.OfficeId == officeId && x.ConversationId == id).ExecuteDeleteAsync(ct);
        return await db.Conversations.Where(x => x.OfficeId == officeId && x.Id == id).ExecuteDeleteAsync(ct) > 0;
    }

    /// <summary>Copy a conversation (title + all messages) into a new one; returns the new id or null.</summary>
    public async Task<Guid?> CloneAsync(Guid officeId, Guid id, Guid createdBy, CancellationToken ct = default)
    {
        var source = await GetAsync(officeId, id, ct);
        if (source is null)
        {
            return null;
        }

        var now = clock.UtcNow;
        var newId = UuidV7.NewId();
        db.Conversations.Add(new ConversationEntity
        {
            Id = newId, OfficeId = officeId, Title = source.Title + " (kopya)", MatterId = source.MatterId,
            CreatedBy = createdBy, CreatedAt = now, UpdatedAt = now,
        });
        var messages = await MessagesAsync(officeId, id, ct);
        foreach (var m in messages)
        {
            db.ChatMessages.Add(new ChatMessageEntity
            {
                Id = UuidV7.NewId(), OfficeId = officeId, ConversationId = newId, Role = m.Role, Content = m.Content, CreatedAt = m.CreatedAt,
            });
        }

        await db.SaveChangesAsync(ct);
        return newId;
    }

    public async Task<SessionBrief> ToBriefAsync(Guid officeId, ConversationEntity c, CancellationToken ct = default)
        => (await ToBriefsAsync(officeId, [c], ct))[0];

    private async Task<IReadOnlyList<SessionBrief>> ToBriefsAsync(Guid officeId, List<ConversationEntity> conversations, CancellationToken ct)
    {
        var authorIds = conversations.Where(c => c.CreatedBy != null).Select(c => c.CreatedBy!.Value).Distinct().ToList();
        var names = authorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking().Where(u => authorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email, ct);

        return conversations.Select(c => new SessionBrief(
            c.Id, c.Title, c.MatterId,
            c.CreatedBy != null && names.TryGetValue(c.CreatedBy.Value, out var n) ? n : null,
            c.UpdatedAt)).ToList();
    }
}

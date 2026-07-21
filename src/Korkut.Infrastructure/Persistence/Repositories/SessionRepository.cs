using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.Agent;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF/Postgres adapter for <see cref="ISessionRepository"/>. Recency is tracked by
/// conversations.updated_at, bumped whenever a message is appended. Tenant context is set
/// by the caller (RLS).
/// </summary>
public sealed class SessionRepository(KorkutDbContext db) : ISessionRepository
{
    public async Task<Guid> CreateAsync(Guid officeId, Guid createdBy, Guid? matterId, DateTimeOffset now, CancellationToken ct = default)
    {
        var conv = new ConversationEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            Title = Session.DefaultTitle,
            MatterId = matterId,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Conversations.Add(conv);
        await db.SaveChangesAsync(ct);
        return conv.Id;
    }

    public async Task<Conversation?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default)
    {
        var c = await db.Conversations.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == id, ct);
        return c is null ? null : ToDomain(c);
    }

    public async Task<(IReadOnlyList<SessionBrief> Items, int Total)> ListAsync(Guid officeId, int limit, int offset, CancellationToken ct = default)
    {
        var baseQuery = db.Conversations.AsNoTracking().Where(x => x.OfficeId == officeId);
        var total = await baseQuery.CountAsync(ct);
        var page = await baseQuery.OrderByDescending(x => x.UpdatedAt).Skip(offset).Take(limit).ToListAsync(ct);
        return (await ToBriefsAsync(page, ct), total);
    }

    public async Task<SessionBrief?> GetBriefAsync(Guid officeId, Guid id, CancellationToken ct = default)
    {
        var c = await db.Conversations.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == id, ct);
        return c is null ? null : (await ToBriefsAsync([c], ct))[0];
    }

    public async Task<IReadOnlyList<ChatTurn>> MessagesAsync(Guid officeId, Guid conversationId, CancellationToken ct = default)
        => await db.ChatMessages.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.ConversationId == conversationId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new ChatTurn(x.Id, x.ConversationId, x.Role, x.Content, x.CreatedAt))
            .ToListAsync(ct);

    public async Task AppendMessageAsync(Guid officeId, Guid conversationId, string role, string content, DateTimeOffset now, CancellationToken ct = default)
    {
        db.ChatMessages.Add(new ChatMessageEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            ConversationId = conversationId,
            Role = role,
            Content = content,
            CreatedAt = now,
        });
        await db.Conversations.Where(x => x.OfficeId == officeId && x.Id == conversationId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, now), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> RenameAsync(Guid officeId, Guid id, string title, DateTimeOffset now, CancellationToken ct = default)
        => await db.Conversations.Where(x => x.OfficeId == officeId && x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Title, title).SetProperty(x => x.UpdatedAt, now), ct) > 0;

    public async Task<bool> SetTitleIfDefaultAsync(Guid officeId, Guid id, string title, CancellationToken ct = default)
        => await db.Conversations.Where(x => x.OfficeId == officeId && x.Id == id && x.Title == Session.DefaultTitle)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Title, title), ct) > 0;

    public async Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default)
    {
        await db.ChatMessages.Where(x => x.OfficeId == officeId && x.ConversationId == id).ExecuteDeleteAsync(ct);
        return await db.Conversations.Where(x => x.OfficeId == officeId && x.Id == id).ExecuteDeleteAsync(ct) > 0;
    }

    public async Task<Guid?> CloneAsync(Guid officeId, Guid id, Guid createdBy, DateTimeOffset now, CancellationToken ct = default)
    {
        var source = await db.Conversations.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == id, ct);
        if (source is null)
        {
            return null;
        }

        var newId = UuidV7.NewId();
        db.Conversations.Add(new ConversationEntity
        {
            Id = newId, OfficeId = officeId, Title = source.Title + " (kopya)", MatterId = source.MatterId,
            CreatedBy = createdBy, CreatedAt = now, UpdatedAt = now,
        });
        var messages = await db.ChatMessages.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.ConversationId == id).OrderBy(x => x.CreatedAt).ToListAsync(ct);
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

    private static Conversation ToDomain(ConversationEntity c) =>
        new(c.Id, c.OfficeId, c.Title, c.MatterId, c.CreatedBy, c.CreatedAt, c.UpdatedAt);

    private async Task<IReadOnlyList<SessionBrief>> ToBriefsAsync(List<ConversationEntity> conversations, CancellationToken ct)
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

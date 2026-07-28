using Korkut.Agent.Chat;
using Korkut.Chat;
using Korkut.Chat.Entities;
using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Chat;

/// <summary>
/// EF/Postgres adapter for <see cref="ISessionRepository"/> over the operational DB — chat is a
/// general operational feature (the conversation thread is a first-class product surface, not
/// agent plumbing), so it lives alongside matters/users. Inherits the tenant-scoped base
/// (<see cref="Repository{TEntity}"/>): reads start from <c>Scoped</c> (office-filtered), so
/// tenancy is enforced by construction on top of RLS. Recency is tracked by
/// conversations.updated_at, bumped whenever a message is appended.
/// </summary>
public sealed class SessionRepository(DbContext db)
    : Repository<Conversation>(db), ISessionRepository
{
    private DbSet<ConversationMessage> Messages => Db.Set<ConversationMessage>();

    private DbSet<Conversation> Conversations => Db.Set<Conversation>();

    public async Task<Guid> CreateAsync(Guid officeId, Guid createdBy, Guid? matterId, DateTimeOffset now, CancellationToken ct = default)
    {
        var conversation = new Conversation(officeId, Session.DefaultTitle, matterId, createdBy, now);
        await AddAsync(conversation, ct);
        return conversation.Id;
    }

    public Task<Conversation?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => FindAsync(officeId, id, ct);

    public async Task<(IReadOnlyList<SessionBrief> Items, int Total)> ListAsync(Guid officeId, int limit, int offset, CancellationToken ct = default)
    {
        var scoped = Scoped(officeId);
        var total = await scoped.CountAsync(ct);
        var page = await scoped.OrderByDescending(x => x.UpdatedAt).Skip(offset).Take(limit).Select(ToBrief).ToListAsync(ct);
        return (page, total);
    }

    public async Task<SessionBrief?> GetBriefAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await Scoped(officeId).Where(x => x.Id == id).Select(ToBrief).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ConversationMessage>> MessagesAsync(Guid officeId, Guid conversationId, CancellationToken ct = default)
        => await Messages.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.ConversationId == conversationId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task AppendMessageAsync(Guid officeId, Guid conversationId, string role, string content, DateTimeOffset now, CancellationToken ct = default)
    {
        Messages.Add(new ConversationMessage(officeId, conversationId, role, content, now));
        await Conversations.Where(x => x.OfficeId == officeId && x.Id == conversationId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, now), ct);
        await Db.SaveChangesAsync(ct);
    }

    public async Task<bool> RenameAsync(Guid officeId, Guid id, string title, DateTimeOffset now, CancellationToken ct = default)
        => await Conversations.Where(x => x.OfficeId == officeId && x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Title, title).SetProperty(x => x.UpdatedAt, now), ct) > 0;

    public async Task<bool> SetTitleIfDefaultAsync(Guid officeId, Guid id, string title, CancellationToken ct = default)
        => await Conversations.Where(x => x.OfficeId == officeId && x.Id == id && x.Title == Session.DefaultTitle)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Title, title), ct) > 0;

    public async Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default)
    {
        await Messages.Where(x => x.OfficeId == officeId && x.ConversationId == id).ExecuteDeleteAsync(ct);
        return await RemoveAsync(officeId, id, ct);
    }

    public async Task<Guid?> CloneAsync(Guid officeId, Guid id, Guid createdBy, DateTimeOffset now, CancellationToken ct = default)
    {
        var source = await FindAsync(officeId, id, ct);
        if (source is null)
        {
            return null;
        }

        var clone = source.CloneFor(createdBy, now);
        Conversations.Add(clone);
        var messages = await Messages.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.ConversationId == id).OrderBy(x => x.CreatedAt).ToListAsync(ct);
        foreach (var m in messages)
        {
            Messages.Add(new ConversationMessage(officeId, clone.Id, m.Role, m.Content, m.CreatedAt));
        }

        await Db.SaveChangesAsync(ct);
        return clone.Id;
    }

    // Projected in-DB. The list read carries the raw CreatedBy id; the host resolves it to a
    // display name when building the response (kept out of the list projection).
    private static readonly System.Linq.Expressions.Expression<Func<Conversation, SessionBrief>> ToBrief =
        c => new SessionBrief(c.Id, c.Title, c.MatterId, c.CreatedBy, c.UpdatedAt);
}

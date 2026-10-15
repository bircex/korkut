using Korkut.Agent.Chat.Abstractions;
using Korkut.Contract.Agent.Chat;
using Korkut.Chat;
using Korkut.Chat.Entities;
using Korkut.Core;
using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Agent.Chat.Repositories;

/// <summary>
/// EF/Postgres adapter for <see cref="ISessionRepository"/> over the operational DB — chat is a
/// general operational feature (the conversation thread is a first-class product surface, not
/// agent plumbing), so it lives alongside matters/users. Inherits the tenant-scoped base
/// (<see cref="Repository{TEntity}"/>): reads start from <c>Scoped</c> (office-filtered), so
/// tenancy is enforced by construction on top of RLS. Recency is tracked by
/// conversations.updated_at, bumped whenever a message is appended.
/// </summary>
internal sealed class SessionRepository(DbContext db)
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

    public async Task<(IReadOnlyList<SessionBrief> Items, int Total)> ListAsync(
        Guid officeId, SessionFilter filter, int limit, int offset, CancellationToken ct = default)
    {
        var ids = filter.MatterIds.ToList();
        var user = filter.UserId;
        var scoped = Scoped(officeId).Where(x =>
            (x.MatterId == null && x.CreatedBy == user)
            || (x.MatterId != null && (filter.AllMatters || ids.Contains(x.MatterId.Value))));
        if (filter.PersonalOnly)
        {
            scoped = scoped.Where(x => x.MatterId == null);
        }
        else if (filter.MatterId is { } matterId)
        {
            scoped = scoped.Where(x => x.MatterId == matterId);
        }

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

    public async Task AppendMessageAsync(Guid officeId, Guid conversationId, string role, string content, DateTimeOffset now, string? sourcesJson = null, CancellationToken ct = default)
    {
        // Postgres' "text" columns reject the NUL byte (22021: invalid byte sequence for encoding
        // "UTF8": 0x00). Content can carry one from a misread upload (e.g. a binary file read as
        // text) — strip it here at the DB boundary so a bad byte can never take the insert down,
        // regardless of which upstream source produced it.
        Messages.Add(new ConversationMessage(officeId, conversationId, role, PostgresTextSanitizer.StripInvalidChars(content), now, sourcesJson));
        await Conversations.Where(x => x.OfficeId == officeId && x.Id == conversationId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, now), ct);
        await Db.SaveChangesAsync(ct);
    }

    public async Task<bool> RenameAsync(Guid officeId, Guid id, string title, DateTimeOffset now, CancellationToken ct = default)
        => await Conversations.Where(x => x.OfficeId == officeId && x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Title, PostgresTextSanitizer.StripInvalidChars(title)).SetProperty(x => x.UpdatedAt, now), ct) > 0;

    // Auto-generated from an LLM summary of the conversation (see SessionChatService), which can
    // itself be derived from ingested document text — same NUL-byte exposure as AppendMessageAsync.
    public async Task<bool> SetTitleIfDefaultAsync(Guid officeId, Guid id, string title, CancellationToken ct = default)
        => await Conversations.Where(x => x.OfficeId == officeId && x.Id == id && x.Title == Session.DefaultTitle)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Title, PostgresTextSanitizer.StripInvalidChars(title)), ct) > 0;

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
            Messages.Add(new ConversationMessage(officeId, clone.Id, m.Role, m.Content, m.CreatedAt, m.SourcesJson));
        }

        await Db.SaveChangesAsync(ct);
        return clone.Id;
    }

    // Projected in-DB. The list read carries the raw CreatedBy id; the host resolves it to a
    // display name when building the response (kept out of the list projection).
    private static readonly System.Linq.Expressions.Expression<Func<Conversation, SessionBrief>> ToBrief =
        c => new SessionBrief(c.Id, c.Title, c.MatterId, c.CreatedBy, c.UpdatedAt);
}

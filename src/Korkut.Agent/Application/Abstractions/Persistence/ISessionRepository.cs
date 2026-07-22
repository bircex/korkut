using Korkut.Agent.Domain.Conversations;
using Korkut.Agent.Domain.Documents;

namespace Korkut.Agent.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for assistant conversations: CRUD, message append/list, and clone.
/// Recency is tracked by <c>updated_at</c>, bumped on every append. Tenant context is set
/// by the caller (RLS). Timestamps are supplied by the service (deterministic/testable).
/// </summary>
public interface ISessionRepository
{
    Task<Guid> CreateAsync(Guid officeId, Guid createdBy, Guid? matterId, DateTimeOffset now, CancellationToken ct = default);

    Task<Conversation?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default);

    /// <summary>A page of conversation briefs (newest first) plus the total count.</summary>
    Task<(IReadOnlyList<SessionBrief> Items, int Total)> ListAsync(Guid officeId, int limit, int offset, CancellationToken ct = default);

    /// <summary>A single conversation's brief, or null if missing.</summary>
    Task<SessionBrief?> GetBriefAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ChatTurn>> MessagesAsync(Guid officeId, Guid conversationId, CancellationToken ct = default);

    Task AppendMessageAsync(Guid officeId, Guid conversationId, string role, string content, DateTimeOffset now, CancellationToken ct = default);

    Task<bool> RenameAsync(Guid officeId, Guid id, string title, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Set the title only if it is still the default placeholder (first-message auto-title).</summary>
    Task<bool> SetTitleIfDefaultAsync(Guid officeId, Guid id, string title, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default);

    /// <summary>Copy a conversation (title + all messages) into a new one; new id, or null if missing.</summary>
    Task<Guid?> CloneAsync(Guid officeId, Guid id, Guid createdBy, DateTimeOffset now, CancellationToken ct = default);
}

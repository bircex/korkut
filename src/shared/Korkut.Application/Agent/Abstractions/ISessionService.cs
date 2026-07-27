using Korkut.Agent.Chat;
using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;

namespace Korkut.Application.Agent.Abstractions;

/// <summary>Assistant-conversation use-cases. Timestamps come from the clock in the implementation.</summary>
public interface ISessionService
{
    Task<Guid> CreateAsync(Guid officeId, Guid createdBy, Guid? matterId, CancellationToken ct = default);

    Task<Conversation?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<(IReadOnlyList<SessionBrief> Items, int Total)> ListAsync(Guid officeId, int limit, int offset, CancellationToken ct = default);

    Task<SessionBrief?> GetBriefAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ConversationMessage>> MessagesAsync(Guid officeId, Guid conversationId, CancellationToken ct = default);

    Task AppendMessageAsync(Guid officeId, Guid conversationId, string role, string content, CancellationToken ct = default);

    Task<bool> RenameAsync(Guid officeId, Guid id, string title, CancellationToken ct = default);

    Task<bool> SetTitleIfDefaultAsync(Guid officeId, Guid id, string title, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<Guid?> CloneAsync(Guid officeId, Guid id, Guid createdBy, CancellationToken ct = default);
}

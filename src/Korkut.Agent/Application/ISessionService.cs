using Korkut.Application.Abstractions.Persistence;
using Korkut.Agent.Domain;
using Korkut.SharedKernel.Time;

namespace Korkut.Agent.Application;

/// <summary>Assistant-conversation use-cases. Timestamps come from the clock here.</summary>
public interface ISessionService
{
    Task<Guid> CreateAsync(Guid officeId, Guid createdBy, Guid? matterId, CancellationToken ct = default);

    Task<Conversation?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<(IReadOnlyList<SessionBrief> Items, int Total)> ListAsync(Guid officeId, int limit, int offset, CancellationToken ct = default);

    Task<SessionBrief?> GetBriefAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ChatTurn>> MessagesAsync(Guid officeId, Guid conversationId, CancellationToken ct = default);

    Task AppendMessageAsync(Guid officeId, Guid conversationId, string role, string content, CancellationToken ct = default);

    Task<bool> RenameAsync(Guid officeId, Guid id, string title, CancellationToken ct = default);

    Task<bool> SetTitleIfDefaultAsync(Guid officeId, Guid id, string title, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<Guid?> CloneAsync(Guid officeId, Guid id, Guid createdBy, CancellationToken ct = default);
}

/// <summary>Session orchestration over the repository port.</summary>
public sealed class SessionService(ISessionRepository repo, IClock clock) : ISessionService
{
    public Task<Guid> CreateAsync(Guid officeId, Guid createdBy, Guid? matterId, CancellationToken ct = default)
        => repo.CreateAsync(officeId, createdBy, matterId, clock.UtcNow, ct);

    public Task<Conversation?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => repo.GetAsync(officeId, id, ct);

    public Task<(IReadOnlyList<SessionBrief> Items, int Total)> ListAsync(Guid officeId, int limit, int offset, CancellationToken ct = default)
        => repo.ListAsync(officeId, limit, offset, ct);

    public Task<SessionBrief?> GetBriefAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => repo.GetBriefAsync(officeId, id, ct);

    public Task<IReadOnlyList<ChatTurn>> MessagesAsync(Guid officeId, Guid conversationId, CancellationToken ct = default)
        => repo.MessagesAsync(officeId, conversationId, ct);

    public Task AppendMessageAsync(Guid officeId, Guid conversationId, string role, string content, CancellationToken ct = default)
        => repo.AppendMessageAsync(officeId, conversationId, role, content, clock.UtcNow, ct);

    public Task<bool> RenameAsync(Guid officeId, Guid id, string title, CancellationToken ct = default)
        => repo.RenameAsync(officeId, id, title, clock.UtcNow, ct);

    public Task<bool> SetTitleIfDefaultAsync(Guid officeId, Guid id, string title, CancellationToken ct = default)
        => repo.SetTitleIfDefaultAsync(officeId, id, title, ct);

    public Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => repo.DeleteAsync(officeId, id, ct);

    public Task<Guid?> CloneAsync(Guid officeId, Guid id, Guid createdBy, CancellationToken ct = default)
        => repo.CloneAsync(officeId, id, createdBy, clock.UtcNow, ct);
}


namespace Korkut.Contract.Agent.Chat;

/// <summary>The session use-cases a controller forwards to: tenant scope + orchestration + DTO mapping live here.</summary>
public interface ISessionAppService
{
    /// <summary>The caller's conversations: their personal ones and their matters', optionally one
    /// matter's (<paramref name="matterId"/>) or personal ones only.</summary>
    Task<SessionPageResponse> ListAsync(int? limit, int? offset, Guid? matterId = null, bool personal = false, CancellationToken ct = default);

    Task<SessionBriefResponse> CreateAsync(Guid? matterId, CancellationToken ct = default);

    Task<SessionDetailResponse> GetAsync(Guid id, CancellationToken ct = default);

    Task<SessionBriefResponse> RenameAsync(Guid id, string title, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<SessionBriefResponse> CloneAsync(Guid id, CancellationToken ct = default);
}

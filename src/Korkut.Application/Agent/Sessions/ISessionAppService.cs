using Korkut.Application.Agent.Contracts;

namespace Korkut.Application.Agent.Sessions;

/// <summary>The session use-cases a controller forwards to: tenant scope + orchestration + DTO mapping live here.</summary>
public interface ISessionAppService
{
    Task<SessionPageResponse> ListAsync(int? limit, int? offset, CancellationToken ct = default);

    Task<SessionBriefResponse> CreateAsync(Guid? matterId, CancellationToken ct = default);

    Task<SessionDetailResponse> GetAsync(Guid id, CancellationToken ct = default);

    Task<SessionBriefResponse> RenameAsync(Guid id, string title, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<SessionBriefResponse> CloneAsync(Guid id, CancellationToken ct = default);
}

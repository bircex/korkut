using Korkut.Agent.Contracts.Sessions;
using Korkut.Agent.Domain.Conversations;
using Korkut.Agent.Services.Core;
using Korkut.Agent.Services.Core.Sessions;
using Korkut.Application.Abstractions.Directory;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Agent;
using Korkut.Web.Errors;

namespace Korkut.Agent.Services.Implementations.Sessions;

/// <summary>
/// Assistant-conversation use-cases as the controller sees them: it owns the tenant scope (RLS),
/// the domain-service calls, author-name resolution, and response-DTO mapping — so the controller
/// only forwards the request and returns the result. Not-found is a thrown <see cref="ApiException"/>
/// (mapped to 404 by the shared handler), never a null the controller must interpret.
/// </summary>
public sealed class SessionAppService(AgentDbContext db, ICurrentUser current, ISessionService svc, IUserLookup dir)
{
    public async Task<SessionPageResponse> ListAsync(int? limit, int? offset, CancellationToken ct = default)
    {
        var take = Math.Clamp(limit ?? 20, 1, 100);
        var skip = Math.Max(offset ?? 0, 0);
        var (items, total) = await ScopedAsync(o => svc.ListAsync(o, take, skip, ct), ct);
        var names = await ResolveNamesAsync(items, ct);
        return new SessionPageResponse(items.Select(b => ToBrief(b, names)).ToList(), total);
    }

    public async Task<SessionBriefResponse> CreateAsync(Guid? matterId, CancellationToken ct = default)
    {
        var brief = await ScopedAsync(async o =>
        {
            var id = await svc.CreateAsync(o, current.UserId, matterId, ct);
            return await svc.GetBriefAsync(o, id, ct);
        }, ct);
        return await ToBriefAsync(brief!, ct);
    }

    public async Task<SessionDetailResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var data = await ScopedAsync(async o =>
        {
            var brief = await svc.GetBriefAsync(o, id, ct);
            if (brief is null)
            {
                return ((SessionBrief Brief, List<ChatMessageResponse> Messages)?)null;
            }

            var messages = (await svc.MessagesAsync(o, id, ct)).Select(m => new ChatMessageResponse(m.Role, m.Content)).ToList();
            return (brief, messages);
        }, ct);

        if (data is not { } d)
        {
            throw ApiException.NotFound("Görüşme bulunamadı.");
        }

        var name = await ResolveNameAsync(d.Brief.CreatedBy, ct);
        return new SessionDetailResponse(d.Brief.Id, d.Brief.Title, d.Brief.MatterId, name, d.Brief.UpdatedAt, d.Messages);
    }

    public async Task<SessionBriefResponse> RenameAsync(Guid id, string title, CancellationToken ct = default)
    {
        var brief = await ScopedAsync(async o =>
            await svc.RenameAsync(o, id, title.Trim(), ct) ? await svc.GetBriefAsync(o, id, ct) : null, ct);
        return brief is null ? throw ApiException.NotFound("Görüşme bulunamadı.") : await ToBriefAsync(brief, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var ok = await ScopedAsync(o => svc.DeleteAsync(o, id, ct), ct);
        if (!ok)
        {
            throw ApiException.NotFound("Görüşme bulunamadı.");
        }
    }

    public async Task<SessionBriefResponse> CloneAsync(Guid id, CancellationToken ct = default)
    {
        var brief = await ScopedAsync(async o =>
        {
            var newId = await svc.CloneAsync(o, id, current.UserId, ct);
            return newId is null ? null : await svc.GetBriefAsync(o, newId.Value, ct);
        }, ct);
        return brief is null ? throw ApiException.NotFound("Görüşme bulunamadı.") : await ToBriefAsync(brief, ct);
    }

    private async Task<T> ScopedAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = current.OfficeId;
        await using var scope = await db.BeginTenantScopeAsync(office, ct);
        var result = await work(office);
        await scope.CommitAsync(ct);
        return result;
    }

    private async Task<SessionBriefResponse> ToBriefAsync(SessionBrief b, CancellationToken ct)
        => new(b.Id, b.Title, b.MatterId, await ResolveNameAsync(b.CreatedBy, ct), b.UpdatedAt);

    private static SessionBriefResponse ToBrief(SessionBrief b, IReadOnlyDictionary<Guid, string> names)
        => new(b.Id, b.Title, b.MatterId, b.CreatedBy is { } id && names.TryGetValue(id, out var n) ? n : null, b.UpdatedAt);

    private async Task<string?> ResolveNameAsync(Guid? author, CancellationToken ct)
    {
        if (author is not { } id)
        {
            return null;
        }

        var names = await dir.DisplayNamesAsync(current.OfficeId, [id], ct);
        return names.TryGetValue(id, out var name) ? name : null;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveNamesAsync(IReadOnlyList<SessionBrief> briefs, CancellationToken ct)
    {
        var ids = briefs.Where(b => b.CreatedBy is not null).Select(b => b.CreatedBy!.Value).Distinct().ToList();
        return await dir.DisplayNamesAsync(current.OfficeId, ids, ct);
    }
}

using Korkut.Domain.Agent.Conversations;
using Korkut.Application.Agent.Sessions;
using Korkut.Core;
using Korkut.Application.Agent.Contracts;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Abstractions;
using ICurrentUser = Korkut.Application.Agent.Abstractions.ICurrentUser;

namespace Korkut.Application.Agent.Sessions;

/// <summary>
/// Session use-cases the controller forwards to: runs the domain service inside a tenant-scoped unit
/// of work (<see cref="IAgentUnitOfWork"/> — the only DbContext holder), resolves author names, and
/// maps to response DTOs. Not-found is a thrown <see cref="NotFoundException"/> (→ 404 by the handler).
/// </summary>
public sealed class SessionAppService(IAgentUnitOfWork uow, ICurrentUser current, ISessionService svc, IUserLookupService dir)
    : ISessionAppService
{
    public async Task<SessionPageResponse> ListAsync(int? limit, int? offset, CancellationToken ct = default)
    {
        var office = current.OfficeId;
        var take = Math.Clamp(limit ?? 20, 1, 100);
        var skip = Math.Max(offset ?? 0, 0);
        var (items, total) = await uow.InScopeAsync(office, () => svc.ListAsync(office, take, skip, ct), ct);
        var names = await ResolveNamesAsync(items, ct);
        return new SessionPageResponse(items.Select(b => ToBrief(b, names)).ToList(), total);
    }

    public async Task<SessionBriefResponse> CreateAsync(Guid? matterId, CancellationToken ct = default)
    {
        var office = current.OfficeId;
        var brief = await uow.InScopeAsync(office, async () =>
        {
            var id = await svc.CreateAsync(office, current.UserId, matterId, ct);
            return await svc.GetBriefAsync(office, id, ct);
        }, ct);
        return await ToBriefAsync(brief!, ct);
    }

    public async Task<SessionDetailResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var office = current.OfficeId;
        var data = await uow.InScopeAsync(office, async () =>
        {
            var brief = await svc.GetBriefAsync(office, id, ct);
            if (brief is null)
            {
                return ((SessionBrief Brief, List<ChatMessageResponse> Messages)?)null;
            }

            var messages = (await svc.MessagesAsync(office, id, ct)).Select(m => new ChatMessageResponse(m.Role, m.Content)).ToList();
            return (brief, messages);
        }, ct);

        if (data is not { } d)
        {
            throw new NotFoundException("Görüşme bulunamadı.");
        }

        var name = await ResolveNameAsync(d.Brief.CreatedBy, ct);
        return new SessionDetailResponse(d.Brief.Id, d.Brief.Title, d.Brief.MatterId, name, d.Brief.UpdatedAt, d.Messages);
    }

    public async Task<SessionBriefResponse> RenameAsync(Guid id, string title, CancellationToken ct = default)
    {
        var office = current.OfficeId;
        var brief = await uow.InScopeAsync(office, async () =>
            await svc.RenameAsync(office, id, title.Trim(), ct) ? await svc.GetBriefAsync(office, id, ct) : null, ct);
        return brief is null ? throw new NotFoundException("Görüşme bulunamadı.") : await ToBriefAsync(brief, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var office = current.OfficeId;
        var ok = await uow.InScopeAsync(office, () => svc.DeleteAsync(office, id, ct), ct);
        if (!ok)
        {
            throw new NotFoundException("Görüşme bulunamadı.");
        }
    }

    public async Task<SessionBriefResponse> CloneAsync(Guid id, CancellationToken ct = default)
    {
        var office = current.OfficeId;
        var brief = await uow.InScopeAsync(office, async () =>
        {
            var newId = await svc.CloneAsync(office, id, current.UserId, ct);
            return newId is null ? null : await svc.GetBriefAsync(office, newId.Value, ct);
        }, ct);
        return brief is null ? throw new NotFoundException("Görüşme bulunamadı.") : await ToBriefAsync(brief, ct);
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

using Korkut.Contract.Audit;
using Korkut.Contract.Organization;
using Korkut.Core;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Audit;

/// <summary>The office's audit trail, newest first, with the actor's name resolved.</summary>
public sealed class AuditQuery(DbContext db, IUserLookup users, IUnitOfWork unitOfWork, ICurrentUser currentUser) : IAuditQuery
{
    public const int MaxPage = 200;

    public Task<AuditPageResponse> ListAsync(int limit, int offset, string? action = null, CancellationToken ct = default)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, async () =>
        {
            var query = db.Set<AuditEvent>().AsNoTracking().Where(e => e.OfficeId == office);
            if (!string.IsNullOrWhiteSpace(action))
            {
                // "client" matches every client.* action; a full name matches just that one.
                var prefix = action.Trim();
                query = query.Where(e => e.Action == prefix || e.Action.StartsWith(prefix + "."));
            }

            var total = await query.CountAsync(ct);
            var rows = await query
                .OrderByDescending(e => e.At)
                .Skip(Math.Max(0, offset))
                .Take(Math.Clamp(limit, 1, MaxPage))
                .ToListAsync(ct);
            var names = await users.DisplayNamesAsync(
                office, rows.Where(r => r.ActorId is not null).Select(r => r.ActorId!.Value).Distinct().ToList(), ct);
            return new AuditPageResponse(
                rows.Select(r => new AuditEventResponse(
                    r.Id, r.At, r.ActorId, r.ActorId is { } a ? names.GetValueOrDefault(a) : null,
                    r.Action, r.EntityType, r.EntityId, r.AfterJson ?? r.BeforeJson)).ToList(),
                total);
        }, ct);
    }
}

using Korkut.Clients.Entities;
using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Clients.Repositories;

/// <summary>
/// EF/Postgres adapter for <see cref="IClientRepository"/>: active clients, by name. Builds on the
/// shared persistence base over the ambient <see cref="DbContext"/> — never the concrete context —
/// so the module stays free of any host's persistence project.
/// </summary>
internal sealed class ClientRepository(DbContext db) : SoftDeleteRepository<Client>(db), IClientRepository
{
    public override async Task<IReadOnlyList<Client>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
}

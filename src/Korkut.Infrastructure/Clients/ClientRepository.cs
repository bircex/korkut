using Korkut.Application.Clients;
using Korkut.Domain.Clients;
using Korkut.Infrastructure.Common;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Clients;

/// <summary>EF/Postgres adapter for <see cref="IClientRepository"/>: active clients, by name.</summary>
public sealed class ClientRepository(KorkutDbContext db) : SoftDeleteRepository<Client>(db), IClientRepository
{
    public override async Task<IReadOnlyList<Client>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
}

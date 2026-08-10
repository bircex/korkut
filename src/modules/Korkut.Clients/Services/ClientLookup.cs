using Korkut.Clients.Entities;
using Korkut.Contract.Clients;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Clients.Services;

/// <summary>
/// Implements the cross-module <see cref="IClientLookup"/> over the module's own table, reading in
/// the caller's already-open tenant scope. Lets other modules name a client without the Client type.
/// </summary>
internal sealed class ClientLookup(DbContext db) : IClientLookup
{
    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(
        Guid officeId, IReadOnlyCollection<Guid> clientIds, CancellationToken ct = default)
    {
        if (clientIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var ids = clientIds.Distinct().ToList();
        return await db.Set<Client>().AsNoTracking()
            .Where(c => c.OfficeId == officeId && ids.Contains(c.Id))
            .Select(c => new { c.Id, c.FullName })
            .ToDictionaryAsync(c => c.Id, c => c.FullName, ct);
    }

    public async Task<int> CountAsync(Guid officeId, CancellationToken ct = default)
        => await db.Set<Client>().AsNoTracking().CountAsync(c => c.OfficeId == officeId && c.DeletedAt == null, ct);

    public async Task<bool> ExistsAsync(Guid officeId, Guid clientId, CancellationToken ct = default)
        => await db.Set<Client>().AsNoTracking().AnyAsync(c => c.OfficeId == officeId && c.Id == clientId, ct);

    public async Task<string?> GetNameAsync(Guid officeId, Guid clientId, CancellationToken ct = default)
        => await db.Set<Client>().AsNoTracking()
            .Where(c => c.OfficeId == officeId && c.Id == clientId)
            .Select(c => c.FullName)
            .FirstOrDefaultAsync(ct);

    public async Task<ClientResponse?> BriefAsync(Guid officeId, Guid clientId, CancellationToken ct = default)
        => await db.Set<Client>().AsNoTracking()
            .Where(c => c.OfficeId == officeId && c.Id == clientId)
            .Select(c => new ClientResponse(
                c.Id, c.FullName, c.Kind, c.Email, c.Phone, c.NationalId, c.Address, c.DateOfBirth, c.CreatedAt))
            .FirstOrDefaultAsync(ct);
}

using Korkut.Application.Settings;
using Korkut.Domain.Settings;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Settings;

/// <summary>
/// EF/Postgres adapter for <see cref="IPersonaRepository"/>. The office is the key, so this
/// one cannot derive from the generic EF base; the read is still pinned to the office
/// before Postgres RLS is consulted.
/// </summary>
public sealed class PersonaRepository(KorkutDbContext db) : IPersonaRepository
{
    public Task<Persona?> FindAsync(Guid officeId, CancellationToken ct = default)
        => db.Personas.AsNoTracking().SingleOrDefaultAsync(p => p.OfficeId == officeId, ct);

    public Task<Persona?> FindForUpdateAsync(Guid officeId, CancellationToken ct = default)
        => db.Personas.SingleOrDefaultAsync(p => p.OfficeId == officeId, ct);

    public Task AddAsync(Persona persona, CancellationToken ct = default)
    {
        db.Personas.Add(persona);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

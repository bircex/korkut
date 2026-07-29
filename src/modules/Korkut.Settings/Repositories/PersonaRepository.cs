using Korkut.Settings.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Settings.Repositories;

/// <summary>EF/Postgres adapter for <see cref="IPersonaRepository"/> over the ambient DbContext (office-keyed).</summary>
internal sealed class PersonaRepository(DbContext db) : IPersonaRepository
{
    public Task<Persona?> FindAsync(Guid officeId, CancellationToken ct = default)
        => db.Set<Persona>().AsNoTracking().SingleOrDefaultAsync(p => p.OfficeId == officeId, ct);

    public Task<Persona?> FindForUpdateAsync(Guid officeId, CancellationToken ct = default)
        => db.Set<Persona>().SingleOrDefaultAsync(p => p.OfficeId == officeId, ct);

    public Task AddAsync(Persona persona, CancellationToken ct = default)
    {
        db.Set<Persona>().Add(persona);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

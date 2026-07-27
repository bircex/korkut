using Korkut.Courts.Repositories;
using Korkut.Courts.Entities;
using Korkut.Contract.Courts;

namespace Korkut.Courts.Services;

/// <summary>
/// Implements the cross-module <see cref="ICourtLookup"/> over the court repository, reading in the
/// caller's already-open tenant scope. Keeps other modules off the Court entity type.
/// </summary>
internal sealed class CourtLookup(ICourtRepository repository) : ICourtLookup
{
    public async Task<string?> GetNameAsync(Guid officeId, Guid courtId, CancellationToken ct = default)
        => (await repository.FindAsync(officeId, courtId, ct))?.Name;
}

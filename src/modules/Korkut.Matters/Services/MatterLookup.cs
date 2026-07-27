using Korkut.Matters.Repositories;
using Korkut.Matters.Entities;
using Korkut.Contract.Matters;

namespace Korkut.Matters.Services;

/// <summary>
/// Implements the cross-module <see cref="IMatterLookup"/> over the matter repository, reading in the
/// caller's already-open tenant scope. Lets Hearings validate the parent without the Matter type.
/// </summary>
internal sealed class MatterLookup(IMatterRepository repository) : IMatterLookup
{
    public async Task<bool> ExistsAsync(Guid officeId, Guid matterId, CancellationToken ct = default)
        => await repository.FindAsync(officeId, matterId, ct) is not null;
}

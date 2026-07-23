using Korkut.Application.Security;
using Korkut.Application.Users;
using Korkut.Domain.Users;
using Korkut.Infrastructure.Common;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Users;

/// <summary>EF/Postgres adapter for <see cref="IUserRepository"/>.</summary>
public sealed class UserRepository(KorkutDbContext db) : Repository<User>(db), IUserRepository
{
    public override async Task<IReadOnlyList<User>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking().OrderBy(u => u.Email).ToListAsync(ct);

    public Task<User?> FindByEmailAsync(Guid officeId, string email, CancellationToken ct = default)
        => Scoped(officeId).AsNoTracking().SingleOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> FindByEmailForUpdateAsync(Guid officeId, string email, CancellationToken ct = default)
        => Scoped(officeId).SingleOrDefaultAsync(u => u.Email == email, ct);

    public Task<bool> ExistsWithEmailAsync(Guid officeId, string email, CancellationToken ct = default)
        => Scoped(officeId).AsNoTracking().AnyAsync(u => u.Email == email, ct);

    public Task<bool> HasAnotherOwnerAsync(Guid officeId, Guid excludingId, CancellationToken ct = default)
        => Scoped(officeId).AsNoTracking()
            .Where(u => u.Role == Roles.OfficeOwner && u.Id != excludingId)
            .AnyAsync(ct);
}

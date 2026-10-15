using Korkut.Clients.Entities;
using Korkut.Clients.Services;
using Korkut.Contract.Matters;
using Korkut.Core;
using Korkut.Courts.Entities;
using Korkut.Courts.Repositories;
using Korkut.Courts.Services;
using Korkut.Hearings.Repositories;
using Korkut.Hearings.Services;
using Korkut.Infrastructure.Persistence;
using Korkut.Matters.Entities;
using Korkut.Matters.Repositories;
using Korkut.Matters.Services;
using Korkut.Organization.Entities;
using Korkut.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Matters.Integration.Tests;

public class MattersTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private Guid _office;

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// These tests run on the superuser context (RLS bypassed) and assert repository/EF behaviour,
    /// so the unit of work only needs to run the work. Opening a real transaction here would hold
    /// locks that contend with the other suites' table resets. RLS itself is covered by
    /// <see cref="TenantIsolationTests"/> and <see cref="TenantMatrixTests"/>.
    /// </summary>
    private sealed class DirectUnitOfWork : IUnitOfWork
    {
        public Task<T> InTenantScopeAsync<T>(Guid officeId, Func<Task<T>> work, CancellationToken ct = default)
            => work();
    }

    /// <summary>The caller the use-case reads its tenant from (normally the access token).</summary>
    private sealed class TestCurrentUser(Guid office, Guid user) : ICurrentUser
    {
        public Guid OfficeId => office;

        public Guid UserId => user;

        // These tests cover matters themselves, not who sees them: the caller sees every matter.
        public IReadOnlySet<string> Permissions => new HashSet<string> { Korkut.Core.Permissions.MattersViewAll };
    }

    private Guid _user;

    public async Task InitializeAsync()
    {
        await pg.ResetAsync();
        _office = Guid.CreateVersion7();
        await using var db = pg.NewContext();
        db.Offices.Add(new Office(_office, "O", DateTimeOffset.UtcNow));
        var user = new Korkut.Organization.Entities.User(_office, "sahip@ornek.av.tr", Korkut.Core.Roles.OfficeOwner, null, null, DateTimeOffset.UtcNow);
        _user = user.Id;
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // Exercise the use-case service over the real EF repositories, so these tests still cover
    // the court-snapshot rule, soft-delete listing, and the tenant-aware composite FKs.
    private MatterService Service(KorkutDbContext db)
    {
        var caller = new TestCurrentUser(_office, _user);
        var members = new MatterMemberRepository(db);
        return new(new MatterRepository(db), members, new MatterAccess(members, caller),
            new HearingLookup(new HearingRepository(db), db), new NoDeadlines(),
            new CourtLookup(new CourtRepository(db)), new ClientLookup(db),
            new DirectUnitOfWork(), caller, new NoAudit(), new Clock());
    }

    // The audit trail has its own tests.
    private sealed class NoAudit : Korkut.Contract.Audit.IAuditLog
    {
        public Task WriteAsync(Korkut.Contract.Audit.AuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    // These tests cover the matter/court/hearing paths, not deadline composition.
    private sealed class NoDeadlines : Korkut.Contract.Deadlines.IDeadlineLookup
    {
        public Task<IReadOnlyList<Korkut.Contract.Deadlines.DeadlineResponse>> ListInRangeAsync(
            Guid officeId, DateTimeOffset? start, DateTimeOffset? end, Guid? matterId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Korkut.Contract.Deadlines.DeadlineResponse>>([]);

        public Task<Korkut.Contract.Deadlines.DeadlineCounts> CountsAsync(
            Guid officeId, DateOnly today, DateOnly until, CancellationToken ct = default)
            => Task.FromResult(new Korkut.Contract.Deadlines.DeadlineCounts(0, 0));
    }

    [Fact]
    public async Task Renaming_a_court_does_not_change_a_matters_historical_snapshot()
    {
        Guid courtId, matterId;
        await using (var db = pg.NewContext())
        {
            var court = new Court(_office, "Ankara 1. Asliye", DateTimeOffset.UtcNow);
            db.Courts.Add(court);
            await db.SaveChangesAsync();
            courtId = court.Id;
            matterId = (await Service(db).CreateAsync(new CreateMatterRequest { Title = "Dava", CourtId = courtId })).Value!.Id;
        }

        // The matter captured the court's title at link time.
        await using (var db = pg.NewContext())
        {
            var m = await db.Matters.AsNoTracking().SingleAsync(x => x.Id == matterId);
            Assert.Equal("Ankara 1. Asliye", m.CourtNameSnapshot);
        }

        // Rename the (mutable) court.
        await using (var db = pg.NewContext())
        {
            await db.Courts.Where(x => x.Id == courtId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, "Ankara 1. Asliye Hukuk"), default);
        }

        // The historical snapshot is unchanged.
        await using (var check = pg.NewContext())
        {
            var m = await check.Matters.AsNoTracking().SingleAsync(x => x.Id == matterId);
            Assert.Equal("Ankara 1. Asliye", m.CourtNameSnapshot);
            Assert.Equal("Ankara 1. Asliye Hukuk", (await check.Courts.AsNoTracking().SingleAsync(x => x.Id == courtId)).Name);
        }
    }

    [Fact]
    public async Task Soft_deleted_matters_are_excluded_from_listings()
    {
        Guid keep, drop;
        await using (var db = pg.NewContext())
        {
            keep = (await Service(db).CreateAsync(new CreateMatterRequest { Title = "Aktif" })).Value!.Id;
            drop = (await Service(db).CreateAsync(new CreateMatterRequest { Title = "Silinen" })).Value!.Id;
            await Service(db).DeleteAsync(drop);
        }

        await using var check = pg.NewContext();
        var listed = (await Service(check).ListAsync()).Value!;
        Assert.Contains(listed, m => m.Id == keep);
        Assert.DoesNotContain(listed, m => m.Id == drop);
        // But the soft-deleted row is retained (not physically gone).
        Assert.NotNull(await check.Matters.AsNoTracking().SingleAsync(x => x.Id == drop));
    }

    [Fact]
    public async Task A_party_cannot_reference_a_matter_in_another_office()
    {
        var otherOffice = Guid.CreateVersion7();
        Guid matterInThisOffice;
        await using (var db = pg.NewContext())
        {
            db.Offices.Add(new Office(otherOffice, "B", DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
            matterInThisOffice = (await Service(db).CreateAsync(new CreateMatterRequest { Title = "M" })).Value!.Id;
        }

        // A party in the OTHER office referencing this office's matter has no matching
        // (office_id, matter_id) parent -> composite FK violation.
        await using var db2 = pg.NewContext();
        db2.Parties.Add(new Party(otherOffice, matterInThisOffice, "counsel", "X", DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task Client_matter_link_uses_the_tenant_aware_composite_key()
    {
        await using var db = pg.NewContext();
        var client = new Client(_office, "Müvekkil", "individual", null, null, null, null, null, DateTimeOffset.UtcNow);
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var matterId = (await Service(db).CreateAsync(new CreateMatterRequest { Title = "Dava", ClientId = client.Id })).Value!.Id;
        var m = await db.Matters.AsNoTracking().SingleAsync(x => x.Id == matterId);
        Assert.Equal(client.Id, m.ClientId);
    }
}

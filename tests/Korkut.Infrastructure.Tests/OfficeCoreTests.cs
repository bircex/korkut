using Korkut.Application.Features.OfficeCore;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Infrastructure.Persistence.Repositories;
using Korkut.Domain.Time;
using Microsoft.EntityFrameworkCore;
using Korkut.Domain.OfficeCore.Clients;

namespace Korkut.Infrastructure.Tests;

public class OfficeCoreTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private Guid _office;

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    public async Task InitializeAsync()
    {
        await pg.ResetAsync();
        _office = Guid.CreateVersion7();
        await using var db = pg.NewContext();
        db.Offices.Add(new OfficeEntity { Id = _office, Name = "O", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // The office-core module is now Clean Architecture: exercise the use-case service over
    // the real EF repositories (not a concrete store), so these tests still cover the
    // court-snapshot rule, soft-delete listing, and the tenant-aware composite FKs.
    private static MatterService Service(KorkutDbContext db) =>
        new(new MatterRepository(db), new HearingRepository(db), new Clock());

    [Fact]
    public async Task Renaming_a_court_does_not_change_a_matters_historical_snapshot()
    {
        Guid courtId, matterId;
        await using (var db = pg.NewContext())
        {
            var court = new CourtEntity { Id = Guid.CreateVersion7(), OfficeId = _office, Name = "Ankara 1. Asliye", CreatedAt = DateTimeOffset.UtcNow };
            db.Courts.Add(court);
            await db.SaveChangesAsync();
            courtId = court.Id;
            matterId = (await Service(db).CreateAsync(_office, "Dava", null, courtId)).Id;
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
            keep = (await Service(db).CreateAsync(_office, "Aktif", null, null)).Id;
            drop = (await Service(db).CreateAsync(_office, "Silinen", null, null)).Id;
            await Service(db).DeleteAsync(_office, drop);
        }

        await using var check = pg.NewContext();
        var listed = await Service(check).ListAsync(_office);
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
            db.Offices.Add(new OfficeEntity { Id = otherOffice, Name = "B", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
            matterInThisOffice = (await Service(db).CreateAsync(_office, "M", null, null)).Id;
        }

        // A party in the OTHER office referencing this office's matter has no matching
        // (office_id, matter_id) parent -> composite FK violation.
        await using var db2 = pg.NewContext();
        db2.Parties.Add(new PartyEntity { Id = Guid.CreateVersion7(), OfficeId = otherOffice, MatterId = matterInThisOffice, Role = "counsel", Name = "X", CreatedAt = DateTimeOffset.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task Client_matter_link_uses_the_tenant_aware_composite_key()
    {
        await using var db = pg.NewContext();
        var client = new Client(_office, "Müvekkil", "individual", null, null, DateTimeOffset.UtcNow);
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var matterId = (await Service(db).CreateAsync(_office, "Dava", client.Id, null)).Id;
        var m = await db.Matters.AsNoTracking().SingleAsync(x => x.Id == matterId);
        Assert.Equal(client.Id, m.ClientId);
    }
}

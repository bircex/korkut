using Korkut.Application.Differential;
using Korkut.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Korkut.Domain.Matters;

namespace Korkut.Infrastructure.Tests;

/// <summary>
/// Data-migration rehearsal against real PostgreSQL (KDM-034): seed an anonymized,
/// production-like snapshot, fingerprint it, re-run the migrator, and prove the
/// migration is idempotent and data-parity holds — all without any real PII/secret
/// entering a test artifact (fingerprints are PII-pseudonymized hashes).
/// </summary>
public class MigrationRehearsalTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private static readonly HashSet<string> Pii = new(StringComparer.Ordinal) { "name", "title" };

    public async Task InitializeAsync() => await pg.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task SeedAnonymizedSnapshotAsync()
    {
        // Synthetic, clearly-fake data standing in for an anonymized production snapshot.
        await using var db = pg.NewContext();
        for (var o = 0; o < 3; o++)
        {
            var officeId = Guid.CreateVersion7();
            db.Offices.Add(new OfficeEntity { Id = officeId, Name = $"Anonim Büro {o}", CreatedAt = DateTimeOffset.UtcNow });
            for (var m = 0; m < 4; m++)
            {
                var matter = new Matter(officeId, $"Anonim Dava {o}-{m}", null, null, null, DateTimeOffset.UtcNow);
                if (m % 2 != 0)
                {
                    matter.Close();
                }

                db.Matters.Add(matter);
            }
        }

        await db.SaveChangesAsync();
    }

    private async Task<List<TableFingerprint>> FingerprintAsync()
    {
        await using var db = pg.NewContext();
        var offices = await db.Offices.AsNoTracking().Select(x => new { x.Id, x.Name }).ToListAsync();
        var matters = await db.Matters.AsNoTracking().Select(x => new { x.Id, x.OfficeId, x.Title, x.Status }).ToListAsync();

        var officeRows = offices.Select(o => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
        {
            ["id"] = o.Id.ToString(), ["name"] = o.Name,
        });
        var matterRows = matters.Select(m => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
        {
            ["id"] = m.Id.ToString(), ["office_id"] = m.OfficeId.ToString(), ["title"] = m.Title, ["status"] = m.Status,
        });

        return
        [
            DataParity.Fingerprint("offices", officeRows, Pii),
            DataParity.Fingerprint("matters", matterRows, Pii),
        ];
    }

    [Fact]
    public async Task Re_running_the_migrator_is_idempotent_and_preserves_data_parity()
    {
        await SeedAnonymizedSnapshotAsync();
        var before = await FingerprintAsync();

        // Re-run the migrator: EF applies no new migrations (idempotent) and must not
        // touch existing rows.
        await using (var db = pg.NewContext())
        {
            await db.Database.MigrateAsync();
            var pending = await db.Database.GetPendingMigrationsAsync();
            Assert.Empty(pending);
        }

        var after = await FingerprintAsync();

        // Row counts + checksums identical → data-parity preserved, migration idempotent.
        Assert.Empty(DataParity.Diff(before, after));
        Assert.Equal(3, before.Single(f => f.Table == "offices").RowCount);
        Assert.Equal(12, before.Single(f => f.Table == "matters").RowCount);
    }

    [Fact]
    public async Task Fingerprints_carry_no_raw_pii()
    {
        await SeedAnonymizedSnapshotAsync();
        var fps = await FingerprintAsync();
        // The seeded PII strings must not survive into any fingerprint checksum.
        Assert.All(fps, f =>
        {
            Assert.DoesNotContain("Anonim Büro", f.Checksum, StringComparison.Ordinal);
            Assert.DoesNotContain("Anonim Dava", f.Checksum, StringComparison.Ordinal);
        });
    }

    // A FIXED dataset with preserved ids — a data migration carries ids over, so both
    // runtimes end up with the same rows. Seeding it in two different orders must yield
    // identical checksums (the core order-independent cross-runtime data-parity claim).
    private static readonly (Guid Id, string Name)[] FixedOffices =
    [
        (new Guid("01930000-0000-7000-8000-000000000001"), "Anonim Büro A"),
        (new Guid("01930000-0000-7000-8000-000000000002"), "Anonim Büro B"),
        (new Guid("01930000-0000-7000-8000-000000000003"), "Anonim Büro C"),
    ];

    private async Task SeedFixedAsync(bool reversed)
    {
        await using var db = pg.NewContext();
        var offices = FixedOffices.Select(o => new OfficeEntity { Id = o.Id, Name = o.Name, CreatedAt = DateTimeOffset.UnixEpoch }).ToList();
        if (reversed)
        {
            offices.Reverse();
        }

        db.Offices.AddRange(offices);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Independent_migrations_of_the_same_snapshot_converge_to_the_same_checksum()
    {
        await SeedFixedAsync(reversed: false);
        var runA = await FingerprintAsync();

        await pg.ResetAsync();
        await SeedFixedAsync(reversed: true);
        var runB = await FingerprintAsync();

        // Same preserved-id rows inserted in a different order → identical checksums.
        Assert.Equal(runA.Single(f => f.Table == "offices").Checksum, runB.Single(f => f.Table == "offices").Checksum);
        Assert.Empty(DataParity.Diff(runA.Where(f => f.Table == "offices"), runB.Where(f => f.Table == "offices")));
    }
}

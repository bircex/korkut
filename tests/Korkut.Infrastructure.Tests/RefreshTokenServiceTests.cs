using Korkut.Organization;
using Korkut.Organization.Entities;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Security;
using Korkut.Core;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Tests;

public class RefreshTokenServiceTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private Guid _office;
    private Guid _user;

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch.AddYears(56);
    }

    private readonly Sha256TokenHasher _tokens = new();
    private readonly Clock _clock = new();

    public async Task InitializeAsync()
    {
        await pg.ResetAsync();
        _office = Guid.CreateVersion7();
        _user = Guid.CreateVersion7();
        await using var db = pg.NewContext();
        db.Offices.Add(new Office(_office, "O", _clock.UtcNow));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private RefreshTokenService Svc(KorkutDbContext db) => new(db, _tokens, _clock);

    [Fact]
    public async Task Rotation_issues_a_new_token_and_invalidates_the_old()
    {
        string first;
        await using (var db = pg.NewAppContext())
        {
            first = (await Svc(db).IssueAsync(_office, _user)).RawToken;
        }

        RotateOutcome rotated;
        await using (var db = pg.NewAppContext())
        {
            rotated = await Svc(db).RotateAsync(first);
        }

        Assert.Equal(RotateStatus.Rotated, rotated.Status);
        Assert.NotNull(rotated.NewRawToken);

        // The new token rotates again fine.
        await using (var db = pg.NewAppContext())
        {
            Assert.Equal(RotateStatus.Rotated, (await Svc(db).RotateAsync(rotated.NewRawToken!)).Status);
        }
    }

    [Fact]
    public async Task Replaying_a_used_token_revokes_the_entire_family()
    {
        string first;
        Guid family;
        await using (var db = pg.NewAppContext())
        {
            var issue = await Svc(db).IssueAsync(_office, _user);
            (first, family) = (issue.RawToken, issue.FamilyId);
        }

        string second;
        await using (var db = pg.NewAppContext())
        {
            second = (await Svc(db).RotateAsync(first)).NewRawToken!; // first now used
        }

        // Replay the already-used first token -> theft detected -> family revoked.
        await using (var db = pg.NewAppContext())
        {
            var reuse = await Svc(db).RotateAsync(first);
            Assert.Equal(RotateStatus.ReuseRevoked, reuse.Status);
            Assert.Equal(family, reuse.FamilyId);
        }

        // The legitimate second token is now revoked too (whole family down).
        await using (var db = pg.NewAppContext())
        {
            var afterRevoke = await Svc(db).RotateAsync(second);
            Assert.Equal(RotateStatus.ReuseRevoked, afterRevoke.Status);
        }

        await using var check = pg.NewContext();
        Assert.True(await check.RefreshTokens.Where(x => x.FamilyId == family).AllAsync(x => x.RevokedAt != null));
    }

    [Fact]
    public async Task Unknown_token_is_invalid()
    {
        await using var db = pg.NewAppContext();
        Assert.Equal(RotateStatus.Invalid, (await Svc(db).RotateAsync("not-a-real-token")).Status);
    }
}

using Korkut.Domain.Users;
using Korkut.Application.Security;
using System.Security.Cryptography;
using Korkut.Application.Auditing;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Infrastructure.Security;
using Korkut.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Tests;

public class TwoFactorServiceTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private Guid _office;
    private Guid _user;

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch.AddYears(56);
    }

    private readonly AesGcmSecretEncryptor _enc = new(RandomNumberGenerator.GetBytes(32));
    private readonly Sha256TokenHasher _tokens = new();
    private readonly Clock _clock = new();

    public async Task InitializeAsync()
    {
        await pg.ResetAsync();
        _office = Guid.CreateVersion7();
        _user = Guid.CreateVersion7();
        await using var db = pg.NewContext();
        db.Offices.Add(new OfficeEntity { Id = _office, Name = "O", CreatedAt = _clock.UtcNow });
        var user = new User(_office, "u@x.test", Roles.Lawyer, null, null, _clock.UtcNow);
        _user = user.Id;
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private TwoFactorService Svc(KorkutDbContext db) => new(db, _enc, _tokens, new EfAuditLog(db, _clock), _clock);

    private async Task<(string uri, byte[] secret)> EnrollAsync()
    {
        await using var db = pg.NewContext();
        var start = await Svc(db).BeginEnrollmentAsync(_office, _user, "u@x.test");
        // Recover the seed from the stored ciphertext to compute live codes in the test.
        var row = await db.UserTotps.AsNoTracking().SingleAsync(x => x.UserId == _user);
        return (start.ProvisioningUri, _enc.Decrypt(row.EncryptedSecret));
    }

    [Fact]
    public async Task Enroll_confirm_issues_recovery_codes_and_enables_user()
    {
        var (_, secret) = await EnrollAsync();
        var code = Totp.Compute(secret, Totp.CurrentStep(_clock.UtcNow));

        await using var db = pg.NewContext();
        var (result, recovery) = await Svc(db).ConfirmEnrollmentAsync(_office, _user, code);

        Assert.Equal(TwoFactorResult.Success, result);
        Assert.Equal(10, recovery.Count);
        Assert.True((await db.Users.AsNoTracking().SingleAsync(x => x.Id == _user)).TwoFactorEnabled);
        Assert.Equal(10, await db.RecoveryCodes.CountAsync());
    }

    [Fact]
    public async Task Challenge_rejects_replay_of_the_same_code()
    {
        var (_, secret) = await EnrollAsync();
        var code = Totp.Compute(secret, Totp.CurrentStep(_clock.UtcNow));
        await using (var db = pg.NewContext())
        {
            await Svc(db).ConfirmEnrollmentAsync(_office, _user, code);
        }

        // A NEW step's code succeeds...
        _clock.UtcNow = _clock.UtcNow.AddSeconds(60);
        var code2 = Totp.Compute(secret, Totp.CurrentStep(_clock.UtcNow));
        await using (var db = pg.NewContext())
        {
            Assert.Equal(TwoFactorResult.Success, await Svc(db).ChallengeAsync(_office, _user, code2));
        }

        // ...replaying that same code (same step) is rejected.
        await using (var db = pg.NewContext())
        {
            Assert.Equal(TwoFactorResult.Replay, await Svc(db).ChallengeAsync(_office, _user, code2));
        }
    }

    [Fact]
    public async Task Recovery_code_is_single_use()
    {
        var (_, secret) = await EnrollAsync();
        var code = Totp.Compute(secret, Totp.CurrentStep(_clock.UtcNow));
        IReadOnlyList<string> recovery;
        await using (var db = pg.NewContext())
        {
            (_, recovery) = await Svc(db).ConfirmEnrollmentAsync(_office, _user, code);
        }

        await using (var db = pg.NewContext())
        {
            Assert.True(await Svc(db).UseRecoveryCodeAsync(_office, _user, recovery[0]));
        }

        await using (var db = pg.NewContext())
        {
            Assert.False(await Svc(db).UseRecoveryCodeAsync(_office, _user, recovery[0])); // reuse rejected
            // and the use was audited
            Assert.True(await db.AuditEvents.AnyAsync(x => x.Action == "2fa.recovery_used"));
        }
    }

    [Fact]
    public async Task Reset_requires_approval_and_revokes_sessions()
    {
        var (_, secret) = await EnrollAsync();
        var code = Totp.Compute(secret, Totp.CurrentStep(_clock.UtcNow));
        Guid stampBefore;
        await using (var db = pg.NewContext())
        {
            await Svc(db).ConfirmEnrollmentAsync(_office, _user, code);
            stampBefore = (await db.Users.AsNoTracking().SingleAsync(x => x.Id == _user)).SecurityStamp;
        }

        await using (var db = pg.NewContext())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Svc(db).ResetAsync(_office, _user, approved: false));
        }

        await using (var db = pg.NewContext())
        {
            await Svc(db).ResetAsync(_office, _user, approved: true);
        }

        await using var check = pg.NewContext();
        var user = await check.Users.AsNoTracking().SingleAsync(x => x.Id == _user);
        Assert.False(user.TwoFactorEnabled);
        Assert.NotEqual(stampBefore, user.SecurityStamp); // sessions revoked
        Assert.Equal(0, await check.UserTotps.CountAsync());
        Assert.True(await check.AuditEvents.AnyAsync(x => x.Action == "2fa.reset"));
    }
}

using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Infrastructure.Security;
using Korkut.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Tests;

public class AuthServiceTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private Guid _office;
    private const string Email = "lawyer@office.test";
    private const string Password = "Correct-Horse-9!";

    private sealed class MutableClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch.AddYears(56);
    }

    private sealed class NullNotifier : ISecurityNotifier
    {
        public int Count { get; private set; }

        public Task NotifyPasswordChangedAsync(Guid officeId, string email, CancellationToken ct)
        {
            Count++;
            return Task.CompletedTask;
        }
    }

    private readonly Pbkdf2PasswordHasher _hasher = new(iterations: 10_000); // faster for tests
    private readonly Sha256TokenHasher _tokens = new();
    private readonly MutableClock _clock = new();
    private NullNotifier _notifier = new();

    public async Task InitializeAsync()
    {
        await pg.ResetAsync();
        _office = Guid.CreateVersion7();
        _notifier = new NullNotifier();
        await using var db = pg.NewContext();
        db.Offices.Add(new OfficeEntity { Id = _office, Name = "O", CreatedAt = _clock.UtcNow });
        db.Users.Add(new UserEntity
        {
            Id = Guid.CreateVersion7(),
            OfficeId = _office,
            Email = Email,
            PasswordHash = _hasher.Hash(Password),
            SecurityStamp = Guid.CreateVersion7(),
            CreatedAt = _clock.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AuthService NewService(KorkutDbContext db) =>
        new(new EfUserDirectory(db), _hasher, _tokens, _notifier, _clock) { MaxFailedAttempts = 3 };

    [Fact]
    public async Task Correct_password_authenticates()
    {
        await using var db = pg.NewContext();
        Assert.Equal(AuthOutcome.Success, await NewService(db).AuthenticateAsync(_office, Email, Password));
    }

    [Fact]
    public async Task Unknown_email_is_indistinguishable_from_wrong_password()
    {
        await using var db = pg.NewContext();
        var svc = NewService(db);
        Assert.Equal(AuthOutcome.InvalidCredentials, await svc.AuthenticateAsync(_office, "nobody@office.test", Password));
        Assert.Equal(AuthOutcome.InvalidCredentials, await svc.AuthenticateAsync(_office, Email, "wrong"));
    }

    [Fact]
    public async Task Repeated_failures_lock_the_account()
    {
        await using var db = pg.NewContext();
        var svc = NewService(db);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(AuthOutcome.InvalidCredentials, await svc.AuthenticateAsync(_office, Email, "wrong"));
        }

        // Even the correct password is refused once locked out.
        Assert.Equal(AuthOutcome.LockedOut, await svc.AuthenticateAsync(_office, Email, Password));
    }

    [Fact]
    public async Task Reset_request_for_unknown_email_creates_no_token_and_does_not_throw()
    {
        await using var db = pg.NewContext();
        await NewService(db).RequestPasswordResetAsync(_office, "ghost@office.test");
        Assert.Equal(0, await db.PasswordResetTokens.CountAsync());
    }

    [Fact]
    public async Task Reset_token_is_single_use_and_replays_fail()
    {
        var raw = await IssueResetTokenAsync();

        await using (var db = pg.NewContext())
        {
            Assert.Equal(ResetOutcome.Success, await NewService(db).ResetPasswordAsync(_office, raw, "New-Password-1!"));
        }

        await using (var db = pg.NewContext())
        {
            // Replay of the same token must fail.
            Assert.Equal(ResetOutcome.InvalidOrExpiredToken, await NewService(db).ResetPasswordAsync(_office, raw, "Another-1!"));
        }
    }

    [Fact]
    public async Task Expired_reset_token_is_rejected()
    {
        var raw = await IssueResetTokenAsync();
        _clock.UtcNow = _clock.UtcNow.AddHours(2); // past the 1h lifetime

        await using var db = pg.NewContext();
        Assert.Equal(ResetOutcome.InvalidOrExpiredToken, await NewService(db).ResetPasswordAsync(_office, raw, "New-1!"));
    }

    [Fact]
    public async Task Successful_reset_rotates_the_security_stamp_and_token_epoch()
    {
        Guid stampBefore;
        int epochBefore;
        await using (var db = pg.NewContext())
        {
            var u = await db.Users.AsNoTracking().SingleAsync(x => x.Email == Email);
            (stampBefore, epochBefore) = (u.SecurityStamp, u.TokenEpoch);
        }

        var raw = await IssueResetTokenAsync();
        await using (var db = pg.NewContext())
        {
            Assert.Equal(ResetOutcome.Success, await NewService(db).ResetPasswordAsync(_office, raw, "New-Password-2!"));
        }

        await using var check = pg.NewContext();
        var after = await check.Users.AsNoTracking().SingleAsync(x => x.Email == Email);
        Assert.NotEqual(stampBefore, after.SecurityStamp);   // sessions revoked
        Assert.Equal(epochBefore + 1, after.TokenEpoch);
        Assert.Equal(1, _notifier.Count);                    // security notification sent
    }

    [Fact]
    public async Task Passwords_and_tokens_are_never_stored_in_plaintext()
    {
        var raw = await IssueResetTokenAsync();
        await using var db = pg.NewContext();
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Email == Email);
        Assert.DoesNotContain(Password, user.PasswordHash!, StringComparison.Ordinal);
        Assert.StartsWith("pbkdf2-sha256$v1", user.PasswordHash);

        var token = await db.PasswordResetTokens.AsNoTracking().SingleAsync();
        Assert.NotEqual(raw, token.TokenHash);               // stored hashed, not raw
    }

    private async Task<string> IssueResetTokenAsync()
    {
        // Reproduce what RequestPasswordReset stores, but capture the raw token here.
        var raw = _tokens.NewRawToken();
        await using var db = pg.NewContext();
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Email == Email);
        await new EfUserDirectory(db).CreateResetTokenAsync(
            user.Id, _office, _tokens.Hash(raw), _clock.UtcNow.AddHours(1), _clock.UtcNow, default);
        return raw;
    }
}

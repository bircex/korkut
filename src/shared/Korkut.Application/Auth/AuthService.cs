using Korkut.Application.Auth.Requests;
using Korkut.Core;
using Korkut.Application.Security;
using Korkut.Organization;

namespace Korkut.Application.Auth;

/// <summary>
/// Local authentication, lockout and password recovery over the user and reset-token
/// repositories. Sign-in is enumeration-safe (a missing account still costs a hash
/// verification, so neither the answer nor the timing gives it away); reset tokens are
/// short-lived, stored hashed and consumed atomically; and a completed reset rotates the
/// security stamp and token epoch, which revokes every existing session.
/// </summary>
public sealed class AuthService(
    IAccountLocator accounts,
    IUserRepository users,
    IPasswordResetTokenRepository tokens,
    IResetTokenResolver resetTokenResolver,
    ITwoFactorChallenge twoFactor,
    IPasswordHasher passwordHasher,
    ITokenHasher tokenHasher,
    ISecurityNotifier notifier,
    IUnitOfWork unitOfWork,
    IClock clock) : IAuthService
{
    public int MaxFailedAttempts { get; init; } = 5;

    public TimeSpan LockoutDuration { get; init; } = TimeSpan.FromMinutes(15);

    public TimeSpan ResetTokenLifetime { get; init; } = TimeSpan.FromHours(1);

    // A fixed dummy hash so verification always runs, even for an unknown user.
    private readonly Lazy<string> _dummyHash = new(() => passwordHasher.Hash("timing-only-not-a-real-password"));

    public async Task<SignInResult> SignInAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = Normalize(request.Email!);
        var office = await LocateSingleAccountAsync(email, ct);
        if (office is null)
        {
            // Nothing to verify against, but still pay the hash cost before answering.
            passwordHasher.Verify(request.Password!, _dummyHash.Value);
            return SignInResult.InvalidCredentials;
        }

        return await unitOfWork.InTenantScopeAsync(office.Value,
            () => AuthenticateAsync(office.Value, email, request.Password!, ct), ct);
    }

    public Task<SessionUser?> FindSessionUserAsync(Guid officeId, Guid userId, CancellationToken ct = default)
        => unitOfWork.InTenantScopeAsync(officeId, async () =>
            ToSessionUser(await users.FindAsync(officeId, userId, ct)), ct);

    public Task<SessionUser?> CompleteTwoFactorAsync(
        Guid officeId, Guid userId, TotpRequest request, CancellationToken ct = default)
        => unitOfWork.InTenantScopeAsync(officeId, async () =>
        {
            var verified = !string.IsNullOrWhiteSpace(request.RecoveryCode)
                ? await twoFactor.UseRecoveryCodeAsync(officeId, userId, request.RecoveryCode!.Trim(), ct)
                : await twoFactor.VerifyCodeAsync(officeId, userId, (request.Code ?? string.Empty).Trim(), ct);

            return verified ? ToSessionUser(await users.FindAsync(officeId, userId, ct)) : null;
        }, ct);

    public async Task RequestPasswordResetAsync(RequestPasswordResetRequest request, CancellationToken ct = default)
    {
        var email = Normalize(request.Email!);
        var office = await LocateSingleAccountAsync(email, ct);
        if (office is null)
        {
            return; // silently no-op: no enumeration signal
        }

        await unitOfWork.InTenantScopeAsync(office.Value, async () =>
        {
            var user = await users.FindByEmailAsync(office.Value, email, ct);
            if (user is null)
            {
                return false;
            }

            var now = clock.UtcNow;
            var rawToken = tokenHasher.NewRawToken();
            await tokens.AddAsync(
                new PasswordResetToken(office.Value, user.Id, tokenHasher.Hash(rawToken), now + ResetTokenLifetime, now), ct);
            // The raw token is delivered out of band (e-mail); it is never logged or audited.
            return true;
        }, ct);
    }

    public async Task<ResetOutcome> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var tokenHash = tokenHasher.Hash(request.Token!);
        var owner = await resetTokenResolver.ResolveAsync(tokenHash, ct);
        if (owner is null)
        {
            return ResetOutcome.InvalidOrExpiredToken;
        }

        return await unitOfWork.InTenantScopeAsync(owner.OfficeId, async () =>
        {
            var now = clock.UtcNow;
            var token = await tokens.FindUsableAsync(owner.OfficeId, tokenHash, now, ct);
            // Single-use: consuming is atomic, so a replay or concurrent reset loses the race.
            if (token is null || !await tokens.TryConsumeAsync(owner.OfficeId, token.Id, now, ct))
            {
                return ResetOutcome.InvalidOrExpiredToken;
            }

            var user = await users.FindForUpdateAsync(owner.OfficeId, token.UserId, ct);
            if (user is null)
            {
                return ResetOutcome.InvalidOrExpiredToken;
            }

            // SetPassword rotates the stamp and epoch → existing sessions are revoked.
            user.SetPassword(passwordHasher.Hash(request.Password!));
            await users.SaveChangesAsync(ct);
            await notifier.NotifyPasswordChangedAsync(owner.OfficeId, user.Email, ct);
            return ResetOutcome.Success;
        }, ct);
    }

    private async Task<SignInResult> AuthenticateAsync(Guid officeId, string email, string password, CancellationToken ct)
    {
        var user = await users.FindByEmailForUpdateAsync(officeId, email, ct);
        var now = clock.UtcNow;

        if (user is not null && user.IsLockedOut(now))
        {
            return SignInResult.LockedOut;
        }

        // Always verify against *some* hash so a missing account is indistinguishable.
        var (verified, needsRehash) = passwordHasher.Verify(password, user?.PasswordHash ?? _dummyHash.Value);
        if (user is null || !verified)
        {
            if (user is not null)
            {
                user.RegisterFailedAttempt(now, MaxFailedAttempts, LockoutDuration);
                await users.SaveChangesAsync(ct);
            }

            return SignInResult.InvalidCredentials;
        }

        user.ClearFailedAttempts();
        if (needsRehash)
        {
            // A transparent upgrade to a stronger hash: the password did not change, so
            // the caller's other sessions stay valid.
            user.RehashPassword(passwordHasher.Hash(password));
        }

        await users.SaveChangesAsync(ct);
        return SignInResult.Success(ToSessionUser(user)!);
    }

    /// <summary>
    /// Sign-in needs exactly one candidate: zero means no such account, and more than one
    /// (the same e-mail in two offices) cannot be disambiguated without asking, which would
    /// itself leak that the accounts exist. Both answer the same way.
    /// </summary>
    private async Task<Guid?> LocateSingleAccountAsync(string email, CancellationToken ct)
    {
        var candidates = await accounts.FindByEmailAsync(email, ct);
        return candidates.Count == 1 ? candidates[0].OfficeId : null;
    }

    private static SessionUser? ToSessionUser(User? user) => user is null
        ? null
        : new SessionUser(user.OfficeId, user.Id, user.Role, user.SecurityStamp, user.TwoFactorEnabled);

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}

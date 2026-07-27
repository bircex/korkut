using Korkut.Organization;
using Korkut.Contract.Organization;
using System.Security.Cryptography;
using Korkut.Application.Security;
using Korkut.Infrastructure.Security;
using Korkut.Core;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Korkut.Api.Tests;

/// <summary>Unit tests for the Google OIDC flow (no database, no live Google).</summary>
public class GoogleOidcTests
{
    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch.AddYears(56);
    }

    // --- PKCE ---

    [Fact]
    public void Pkce_challenge_is_base64url_sha256_of_the_verifier()
    {
        var challenge = OidcChallenge.Create();
        var hash = SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(challenge.CodeVerifier));
        var expected = Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.Equal(expected, challenge.CodeChallenge);
        Assert.DoesNotContain('=', challenge.CodeChallenge);
        Assert.DoesNotContain('+', challenge.CodeChallenge);
    }

    [Fact]
    public void Each_challenge_has_unique_state_nonce_verifier()
    {
        var a = OidcChallenge.Create();
        var b = OidcChallenge.Create();
        Assert.NotEqual(a.State, b.State);
        Assert.NotEqual(a.Nonce, b.Nonce);
        Assert.NotEqual(a.CodeVerifier, b.CodeVerifier);
    }

    // --- ID token validation (real RS256 sign + verify with a test key) ---

    private static (string token, GoogleIdTokenValidator validator) SignedToken(
        string aud, string nonce, bool emailVerified = true, DateTime? expires = null, RsaSecurityKey? signWith = null)
    {
        var key = signWith ?? new RsaSecurityKey(RSA.Create(2048)) { KeyId = "test" };
        var handler = new JsonWebTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "https://accounts.google.com",
            Audience = aud,
            Expires = expires ?? DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = "google-123",
                ["email"] = "user@example.com",
                ["email_verified"] = emailVerified,
                ["nonce"] = nonce,
            },
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
        };
        var token = handler.CreateToken(descriptor);
        var validator = new GoogleIdTokenValidator("korkut-client", () => [key]);
        return (token, validator);
    }

    [Fact]
    public async Task Valid_token_yields_identity()
    {
        var (token, validator) = SignedToken("korkut-client", "n1");
        var id = await validator.ValidateAsync(token);
        Assert.Equal("google-123", id.Subject);
        Assert.True(id.EmailVerified);
        Assert.Equal("n1", id.Nonce);
    }

    [Fact]
    public async Task Wrong_audience_is_rejected()
    {
        var (token, _) = SignedToken("someone-else", "n1");
        var validator = new GoogleIdTokenValidator("korkut-client", () => [new RsaSecurityKey(RSA.Create(2048))]);
        await Assert.ThrowsAnyAsync<Exception>(() => validator.ValidateAsync(token));
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var (token, validator) = SignedToken("korkut-client", "n1", expires: DateTime.UtcNow.AddMinutes(-10));
        await Assert.ThrowsAnyAsync<Exception>(() => validator.ValidateAsync(token));
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var (token, _) = SignedToken("korkut-client", "n1");
        var otherValidator = new GoogleIdTokenValidator("korkut-client", () => [new RsaSecurityKey(RSA.Create(2048))]);
        await Assert.ThrowsAnyAsync<Exception>(() => otherValidator.ValidateAsync(token));
    }

    // --- Sign-in flow with fakes ---

    private sealed class FakeExchanger(string idToken) : IOidcTokenExchanger
    {
        public Task<string> ExchangeForIdTokenAsync(string code, string codeVerifier, CancellationToken ct = default)
            => Task.FromResult(idToken);
    }

    private sealed class FakeValidator(GoogleIdentity id) : IGoogleIdTokenValidator
    {
        public Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken ct = default) => Task.FromResult(id);
    }

    /// <summary>Stands in for both cross-tenant lookups: by Google subject and by e-mail.</summary>
    private sealed class FakeDirectory : IGoogleAccountDirectory, IAccountLocator
    {
        public List<AccountLocation> BySub { get; } = [];
        public List<AccountLocation> ByEmail { get; } = [];
        public HashSet<Guid> DisabledOffices { get; } = [];
        public HashSet<Guid> TwoFactorUsers { get; } = [];
        public List<(Guid, string)> Linked { get; } = [];

        public Task<AccountLocation?> FindByGoogleSubAsync(string s, CancellationToken ct = default) => Task.FromResult(BySub.Find(u => true));
        public Task<IReadOnlyList<AccountLocation>> FindByEmailAsync(string e, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<AccountLocation>>(ByEmail);
        public Task LinkGoogleSubAsync(Guid userId, string s, CancellationToken ct = default) { Linked.Add((userId, s)); return Task.CompletedTask; }
        public Task<bool> IsOfficeDisabledAsync(Guid o, CancellationToken ct = default) => Task.FromResult(DisabledOffices.Contains(o));
        public Task<bool> OfficeRequiresTwoFactorAsync(Guid o, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> UserHasTwoFactorAsync(Guid u, CancellationToken ct = default) => Task.FromResult(TwoFactorUsers.Contains(u));
    }

    /// <summary>Only the session lookup matters here; the local sign-in flow is tested elsewhere.</summary>
    private sealed class FakeAuth : IAuthService
    {
        public Task<SessionUser?> FindSessionUserAsync(Guid officeId, Guid userId, CancellationToken ct = default)
            => Task.FromResult<SessionUser?>(new SessionUser(officeId, userId, Roles.Lawyer, Guid.CreateVersion7(), false));

        public Task<SignInResult> SignInAsync(LoginRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<SessionUser?> CompleteTwoFactorAsync(Guid officeId, Guid userId, TotpRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task RequestPasswordResetAsync(RequestPasswordResetRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<ResetOutcome> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private static readonly GoogleOidcOptions Options = new()
    {
        ClientId = "korkut-client", ClientSecret = "s", RedirectUri = "https://app/callback",
    };

    private static GoogleSignInService Service(GoogleIdentity identity, FakeDirectory dir, IOidcFlowStore store, Clock clock)
        => new(Options, store, new FakeExchanger("tok"), new FakeValidator(identity), dir, dir, new FakeAuth(), clock);

    private static AccountLocation User(Guid id, Guid office) => new(id, office, "user@example.com");

    [Fact]
    public async Task Replayed_callback_is_rejected()
    {
        var store = new InMemoryOidcFlowStore();
        var clock = new Clock();
        var dir = new FakeDirectory();
        var svc = new GoogleSignInService(Options, store, new FakeExchanger("t"),
            new FakeValidator(new GoogleIdentity("google-123", "user@example.com", true, "n1")), dir, dir, new FakeAuth(), clock);

        var challenge = OidcChallenge.Create() with { Nonce = "n1" };
        await store.SaveAsync(challenge, clock.UtcNow.AddMinutes(5));

        dir.BySub.Add(User(Guid.CreateVersion7(), Guid.CreateVersion7()));
        var first = await svc.CompleteAsync(challenge.State, "code");
        var replay = await svc.CompleteAsync(challenge.State, "code"); // same state again

        Assert.Equal(GoogleSignInStatus.Authenticated, first.Status);
        Assert.Equal(GoogleSignInStatus.Rejected, replay.Status);
        Assert.Equal("invalid_state", replay.Reason);
    }

    [Theory]
    [InlineData("bad-nonce", true, "nonce_mismatch")]
    [InlineData("n1", false, "unverified_email")]
    public async Task Callback_rejects_bad_nonce_or_unverified_email(string tokenNonce, bool verified, string expected)
    {
        var store = new InMemoryOidcFlowStore();
        var clock = new Clock();
        var challenge = OidcChallenge.Create() with { Nonce = "n1" };
        await store.SaveAsync(challenge, clock.UtcNow.AddMinutes(5));
        var svc = Service(new GoogleIdentity("google-123", "user@example.com", verified, tokenNonce), new FakeDirectory(), store, clock);

        var result = await svc.CompleteAsync(challenge.State, "code");
        Assert.Equal(GoogleSignInStatus.Rejected, result.Status);
        Assert.Equal(expected, result.Reason);
    }

    [Fact]
    public async Task First_time_email_match_links_by_subject()
    {
        var store = new InMemoryOidcFlowStore();
        var clock = new Clock();
        var challenge = OidcChallenge.Create() with { Nonce = "n1" };
        await store.SaveAsync(challenge, clock.UtcNow.AddMinutes(5));
        var dir = new FakeDirectory();
        var uid = Guid.CreateVersion7();
        dir.ByEmail.Add(User(uid, Guid.CreateVersion7()));
        var svc = Service(new GoogleIdentity("google-123", "user@example.com", true, "n1"), dir, store, clock);

        var result = await svc.CompleteAsync(challenge.State, "code");
        Assert.Equal(GoogleSignInStatus.Authenticated, result.Status);
        Assert.Contains((uid, "google-123"), dir.Linked); // linked by subject
    }

    [Fact]
    public async Task Ambiguous_email_across_offices_is_rejected()
    {
        var store = new InMemoryOidcFlowStore();
        var clock = new Clock();
        var challenge = OidcChallenge.Create() with { Nonce = "n1" };
        await store.SaveAsync(challenge, clock.UtcNow.AddMinutes(5));
        var dir = new FakeDirectory();
        dir.ByEmail.Add(User(Guid.CreateVersion7(), Guid.CreateVersion7()));
        dir.ByEmail.Add(User(Guid.CreateVersion7(), Guid.CreateVersion7()));
        var svc = Service(new GoogleIdentity("google-123", "user@example.com", true, "n1"), dir, store, clock);

        var result = await svc.CompleteAsync(challenge.State, "code");
        Assert.Equal("ambiguous_link", result.Reason);
    }

    [Fact]
    public async Task Disabled_office_is_rejected()
    {
        var store = new InMemoryOidcFlowStore();
        var clock = new Clock();
        var challenge = OidcChallenge.Create() with { Nonce = "n1" };
        await store.SaveAsync(challenge, clock.UtcNow.AddMinutes(5));
        var dir = new FakeDirectory();
        var office = Guid.CreateVersion7();
        dir.BySub.Add(User(Guid.CreateVersion7(), office));
        dir.DisabledOffices.Add(office);
        var svc = Service(new GoogleIdentity("google-123", "user@example.com", true, "n1"), dir, store, clock);

        var result = await svc.CompleteAsync(challenge.State, "code");
        Assert.Equal("office_disabled", result.Reason);
    }

    [Fact]
    public async Task Two_factor_user_gets_two_factor_required_not_a_session()
    {
        var store = new InMemoryOidcFlowStore();
        var clock = new Clock();
        var challenge = OidcChallenge.Create() with { Nonce = "n1" };
        await store.SaveAsync(challenge, clock.UtcNow.AddMinutes(5));
        var dir = new FakeDirectory();
        var uid = Guid.CreateVersion7();
        dir.BySub.Add(User(uid, Guid.CreateVersion7()));
        dir.TwoFactorUsers.Add(uid);
        var svc = Service(new GoogleIdentity("google-123", "user@example.com", true, "n1"), dir, store, clock);

        var result = await svc.CompleteAsync(challenge.State, "code");
        Assert.Equal(GoogleSignInStatus.TwoFactorRequired, result.Status); // NOT Authenticated
    }
}

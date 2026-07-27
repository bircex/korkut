using Korkut.Contract.Organization;
using Korkut.Core;

namespace Korkut.Organization;

/// <summary>
/// Google OIDC sign-in over the OIDC ports and the cross-tenant account lookups. Every
/// rejection is a reason string rather than an exception, because the callback always ends
/// as a redirect the browser can follow.
/// </summary>
public sealed class GoogleSignInService(
    GoogleOidcOptions options,
    IOidcFlowStore flowStore,
    IOidcTokenExchanger tokenExchanger,
    IGoogleIdTokenValidator idTokenValidator,
    IGoogleAccountDirectory directory,
    IAccountLocator accounts,
    IAuthService auth,
    IClock clock) : IGoogleSignInService
{
    public TimeSpan ChallengeLifetime { get; init; } = TimeSpan.FromMinutes(10);

    public bool IsConfigured => options.IsConfigured;

    public async Task<string> BeginAsync(CancellationToken ct = default)
    {
        if (!options.IsConfigured)
        {
            throw new InvalidOperationException("Google OIDC is not configured.");
        }

        var challenge = OidcChallenge.Create();
        await flowStore.SaveAsync(challenge, clock.UtcNow + ChallengeLifetime, ct);

        var pairs = new (string Key, string Value)[]
        {
            ("client_id", options.ClientId),
            ("redirect_uri", options.RedirectUri), // exact, pre-registered
            ("response_type", "code"),
            ("scope", "openid email profile"),
            ("state", challenge.State),
            ("nonce", challenge.Nonce),
            ("code_challenge", challenge.CodeChallenge),
            ("code_challenge_method", "S256"),
            ("access_type", "online"),
            ("prompt", "select_account"),
        };
        var query = string.Join('&', pairs.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        return $"https://accounts.google.com/o/oauth2/v2/auth?{query}";
    }

    public async Task<GoogleSignInResult> CompleteAsync(string state, string code, CancellationToken ct = default)
    {
        // Single-use consume: a replayed callback finds no challenge and is rejected.
        var challenge = await flowStore.ConsumeAsync(state, clock.UtcNow, ct);
        if (challenge is null)
        {
            return GoogleSignInResult.Reject("invalid_state");
        }

        var idToken = await tokenExchanger.ExchangeForIdTokenAsync(code, challenge.CodeVerifier, ct);

        GoogleIdentity identity;
        try
        {
            identity = await idTokenValidator.ValidateAsync(idToken, ct);
        }
        catch (Exception)
        {
            return GoogleSignInResult.Reject("invalid_id_token");
        }

        if (!string.Equals(identity.Nonce, challenge.Nonce, StringComparison.Ordinal))
        {
            return GoogleSignInResult.Reject("nonce_mismatch");
        }

        if (!identity.EmailVerified)
        {
            return GoogleSignInResult.Reject("unverified_email");
        }

        var resolution = await ResolveAccountAsync(identity, ct);
        if (resolution.Reason == "no_account")
        {
            // Verified e-mail with no Korkut account → eligible for self-serve signup.
            return GoogleSignInResult.Signup(Normalize(identity.Email), identity.Subject);
        }

        if (resolution.Reason is not null)
        {
            return GoogleSignInResult.Reject(resolution.Reason);
        }

        var account = resolution.Account!;
        if (await directory.IsOfficeDisabledAsync(account.OfficeId, ct))
        {
            return GoogleSignInResult.Reject("office_disabled");
        }

        var twoFactor = await directory.OfficeRequiresTwoFactorAsync(account.OfficeId, ct)
            || await directory.UserHasTwoFactorAsync(account.UserId, ct);

        var user = await auth.FindSessionUserAsync(account.OfficeId, account.UserId, ct);
        return user is null
            ? GoogleSignInResult.Reject("no_account")
            : GoogleSignInResult.SignedIn(user, twoFactor);
    }

    private sealed record Resolution(AccountLocation? Account, string? Reason);

    private async Task<Resolution> ResolveAccountAsync(GoogleIdentity identity, CancellationToken ct)
    {
        // 1. The link is by provider subject — the stable, unambiguous identity.
        var bySub = await directory.FindByGoogleSubAsync(identity.Subject, ct);
        if (bySub is not null)
        {
            return new Resolution(bySub, null);
        }

        // 2. First-time linking: match a single existing (unlinked) account by verified e-mail.
        var byEmail = await accounts.FindByEmailAsync(Normalize(identity.Email), ct);
        if (byEmail.Count == 0)
        {
            return new Resolution(null, "no_account");
        }

        if (byEmail.Count > 1)
        {
            return new Resolution(null, "ambiguous_link"); // same e-mail in multiple offices
        }

        await directory.LinkGoogleSubAsync(byEmail[0].UserId, identity.Subject, ct);
        return new Resolution(byEmail[0], null);
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}

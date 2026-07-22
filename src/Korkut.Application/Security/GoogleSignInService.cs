using Korkut.Domain.Time;

namespace Korkut.Application.Security;

/// <summary>
/// Google OIDC sign-in: authorization-code + PKCE + state + nonce, exact redirect
/// URI, deterministic account linking (by provider subject, email only for
/// first-time linking), and a 2FA policy gate. The callback NEVER mints a full
/// session — it returns <see cref="GoogleSignInStatus"/>; session issuance is
/// KDM-018 and only proceeds when 2FA (if required) is complete.
/// </summary>
public sealed class GoogleSignInService(
    GoogleOidcOptions options,
    IOidcFlowStore flowStore,
    IOidcTokenExchanger tokenExchanger,
    IGoogleIdTokenValidator idTokenValidator,
    IGoogleUserDirectory directory,
    IClock clock)
{
    public TimeSpan ChallengeLifetime { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Begin the flow: persist a single-use challenge and return the Google auth URL.</summary>
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

    /// <summary>Complete the callback. State must match a stored, unused challenge (replay-safe).</summary>
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

        var resolution = await ResolveUserAsync(identity, ct);
        if (resolution.Reason == "no_account")
        {
            // Verified email with no Korkut account → eligible for self-serve signup.
            return GoogleSignInResult.Signup(identity.Email.Trim().ToLowerInvariant(), identity.Subject);
        }

        if (resolution.Reason is not null)
        {
            return GoogleSignInResult.Reject(resolution.Reason);
        }

        var resolved = resolution.User!;
        if (await directory.IsOfficeDisabledAsync(resolved.OfficeId, ct))
        {
            return GoogleSignInResult.Reject("office_disabled");
        }

        var twoFactor = await directory.OfficeRequiresTwoFactorAsync(resolved.OfficeId, ct)
            || await directory.UserHasTwoFactorAsync(resolved.Id, ct);

        return new GoogleSignInResult(
            twoFactor ? GoogleSignInStatus.TwoFactorRequired : GoogleSignInStatus.Authenticated,
            resolved.Id, resolved.OfficeId, null);
    }

    private sealed record Resolution(AuthUser? User, string? Reason);

    private async Task<Resolution> ResolveUserAsync(GoogleIdentity identity, CancellationToken ct)
    {
        // 1. Link is by provider subject — the stable, unambiguous identity.
        var bySub = await directory.FindByGoogleSubAsync(identity.Subject, ct);
        if (bySub is not null)
        {
            return new Resolution(bySub, null);
        }

        // 2. First-time linking: match a single existing (unlinked) account by verified email.
        var email = identity.Email.Trim().ToLowerInvariant();
        var byEmail = await directory.FindByEmailAcrossOfficesAsync(email, ct);
        if (byEmail.Count == 0)
        {
            return new Resolution(null, "no_account");
        }

        if (byEmail.Count > 1)
        {
            return new Resolution(null, "ambiguous_link"); // same email in multiple offices
        }

        await directory.LinkGoogleSubAsync(byEmail[0].Id, identity.Subject, ct);
        return new Resolution(byEmail[0], null);
    }
}

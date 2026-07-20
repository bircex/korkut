namespace Korkut.Application.Security;

public enum AdminSignInStatus
{
    /// <summary>Allowlisted + TOTP enrolled — proceed to a Korkut TOTP challenge (never a session yet).</summary>
    TwoFactorRequired,

    /// <summary>Allowlisted but no TOTP yet — enrollment needs a bootstrap token or admin approval.</summary>
    BootstrapRequired,

    /// <summary>Rejected — email not on the compiled allowlist, or the flow was invalid.</summary>
    Rejected,
}

public sealed record AdminSignInResult(AdminSignInStatus Status, string? Email, string? Reason);

/// <summary>Tracks whether an allowlisted admin has completed Korkut TOTP enrollment.</summary>
public interface IAdminEnrollment
{
    Task<bool> IsEnrolledAsync(string normalizedEmail, CancellationToken ct);
}

/// <summary>
/// Platform-admin sign-in: Google SSO only (no local password/reset path exists),
/// gated by the compiled <see cref="AdminAllowlist"/>, and ALWAYS followed by a
/// mandatory Korkut TOTP step — the Google callback alone never yields an admin
/// session. Unenrolled admins are sent through bootstrap/approval, not straight in.
/// </summary>
public sealed class AdminSignInService(
    IOidcFlowStore flowStore,
    IOidcTokenExchanger tokenExchanger,
    IGoogleIdTokenValidator idTokenValidator,
    IAdminEnrollment enrollment)
{
    public async Task<AdminSignInResult> CompleteAsync(string state, string code, CancellationToken ct = default)
    {
        var challenge = await flowStore.ConsumeAsync(state, DateTimeOffset.UtcNow, ct);
        if (challenge is null)
        {
            return new AdminSignInResult(AdminSignInStatus.Rejected, null, "invalid_state");
        }

        GoogleIdentity identity;
        try
        {
            var idToken = await tokenExchanger.ExchangeForIdTokenAsync(code, challenge.CodeVerifier, ct);
            identity = await idTokenValidator.ValidateAsync(idToken, ct);
        }
        catch (Exception)
        {
            return new AdminSignInResult(AdminSignInStatus.Rejected, null, "invalid_id_token");
        }

        if (!string.Equals(identity.Nonce, challenge.Nonce, StringComparison.Ordinal))
        {
            return new AdminSignInResult(AdminSignInStatus.Rejected, null, "nonce_mismatch");
        }

        if (!identity.EmailVerified)
        {
            return new AdminSignInResult(AdminSignInStatus.Rejected, null, "unverified_email");
        }

        // The allowlist is the gate — a Google-verified but non-allowlisted email is rejected.
        if (!AdminAllowlist.IsAllowed(identity.Email))
        {
            return new AdminSignInResult(AdminSignInStatus.Rejected, null, "not_allowlisted");
        }

        var email = AdminAllowlist.Normalize(identity.Email);
        var enrolled = await enrollment.IsEnrolledAsync(email, ct);
        return enrolled
            ? new AdminSignInResult(AdminSignInStatus.TwoFactorRequired, email, null)
            : new AdminSignInResult(AdminSignInStatus.BootstrapRequired, email, null);
    }
}

using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Organization;

// ---- Sign-in / session result types ----

/// <summary>How a sign-in attempt ended.</summary>
public enum AuthOutcome { Success, InvalidCredentials, LockedOut, OfficePending }

/// <summary>How a password reset ended.</summary>
public enum ResetOutcome { Success, InvalidOrExpiredToken }

/// <summary>What a session carries about its owner: identity, permissions, and the revoking stamp.</summary>
public sealed record SessionUser(Guid OfficeId, Guid UserId, string Role, Guid SecurityStamp, bool TwoFactorEnabled);

/// <summary>The result of a sign-in attempt. <see cref="User"/> is set only on success.</summary>
public sealed record SignInResult(AuthOutcome Outcome, SessionUser? User)
{
    public static readonly SignInResult InvalidCredentials = new(AuthOutcome.InvalidCredentials, null);
    public static readonly SignInResult LockedOut = new(AuthOutcome.LockedOut, null);

    /// <summary>Credentials were valid, but the office is still disabled (awaiting a platform admin).</summary>
    public static readonly SignInResult OfficePending = new(AuthOutcome.OfficePending, null);

    public static SignInResult Success(SessionUser user) => new(AuthOutcome.Success, user);
}

// ---- Request bodies ----

/// <summary>Body for a local (e-mail + password) sign-in.</summary>
public sealed record LoginRequest
{
    /// <summary>A member's e-mail, or the installation admin's user name (<see cref="InstallationAdmin.UserName"/>).</summary>
    [Required(ErrorMessage = "E-posta ya da kullanıcı adı zorunludur.")]
    [MaxLength(320, ErrorMessage = "E-posta ya da kullanıcı adı en fazla 320 karakter olabilir.")]
    public string? Email { get; init; }

    [Required(ErrorMessage = "Parola zorunludur.")]
    public string? Password { get; init; }
}

/// <summary>Body for the second sign-in step: either the authenticator code or a recovery code.</summary>
public sealed record TotpRequest
{
    public string? Code { get; init; }
    public string? RecoveryCode { get; init; }
}

/// <summary>Body for asking for a password-reset link.</summary>
public sealed record RequestPasswordResetRequest
{
    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    public string? Email { get; init; }
}

/// <summary>Body for setting a new password with a reset token.</summary>
public sealed record ResetPasswordRequest
{
    [Required(ErrorMessage = "Bağlantı kodu zorunludur.")]
    public string? Token { get; init; }

    [Required(ErrorMessage = "Parola zorunludur.")]
    [MinLength(10, ErrorMessage = "Parola en az 10 karakter olmalıdır.")]
    public string? Password { get; init; }
}

/// <summary>The signed-in member changing their own password.</summary>
public sealed record ChangePasswordRequest
{
    [Required(ErrorMessage = "Mevcut parola zorunludur.")]
    public string? CurrentPassword { get; init; }

    [Required(ErrorMessage = "Yeni parola zorunludur.")]
    [MinLength(10, ErrorMessage = "Parola en az 10 karakter olmalıdır.")]
    public string? NewPassword { get; init; }
}

/// <summary>
/// The account every installation starts with: the office owner the installer creates, who signs in
/// with this user name and the password the installer generated, and invites everyone else.
/// </summary>
public static class InstallationAdmin
{
    public const string UserName = "korkut-admin";
}

/// <summary>
/// Local sign-in, lockout and password recovery (cross-module contract). Every method is reachable
/// without a session and gives nothing away: wrong password == unknown account, and a reset request
/// reveals nothing about existence.
/// </summary>
public interface IAuthService
{
    Task<SignInResult> SignInAsync(LoginRequest request, CancellationToken ct = default);

    Task<SessionUser?> FindSessionUserAsync(Guid officeId, Guid userId, CancellationToken ct = default);

    Task<SessionUser?> CompleteTwoFactorAsync(Guid officeId, Guid userId, TotpRequest request, CancellationToken ct = default);

    Task RequestPasswordResetAsync(RequestPasswordResetRequest request, CancellationToken ct = default);

    Task<ResetOutcome> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);

    /// <summary>The signed-in member sets a new password; null when the current one is wrong. Every
    /// other session ends (the stamp rotates), so the caller reissues this one.</summary>
    Task<SessionUser?> ChangePasswordAsync(Guid officeId, Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
}

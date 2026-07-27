using Korkut.Organization.Entities;

namespace Korkut.Organization;

/// <summary>
/// The cross-tenant lookups Google sign-in needs before any session exists: which account a
/// provider subject belongs to, whether its office is allowed to sign in at all, and
/// whether a second factor is required. Finding an account by e-mail is
/// <see cref="IAccountLocator"/>'s job — the same lookup local sign-in uses.
/// </summary>
public interface IGoogleAccountDirectory
{
    Task<AccountLocation?> FindByGoogleSubAsync(string googleSub, CancellationToken ct = default);

    /// <summary>Links a verified Google subject to an existing account (first sign-in only).</summary>
    Task LinkGoogleSubAsync(Guid userId, string googleSub, CancellationToken ct = default);

    Task<bool> IsOfficeDisabledAsync(Guid officeId, CancellationToken ct = default);

    Task<bool> OfficeRequiresTwoFactorAsync(Guid officeId, CancellationToken ct = default);

    Task<bool> UserHasTwoFactorAsync(Guid userId, CancellationToken ct = default);
}

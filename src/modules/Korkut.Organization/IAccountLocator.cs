using Korkut.Organization.Entities;

namespace Korkut.Organization;

/// <summary>
/// Finds which office(s) an e-mail has an account in, before any session exists. Sign-in
/// cannot be tenant-scoped until this answers, so the adapter is the one sanctioned
/// RLS bypass for the flow.
/// </summary>
public interface IAccountLocator
{
    /// <summary>Every account with this e-mail, across offices. Empty when there is none.</summary>
    Task<IReadOnlyList<AccountLocation>> FindByEmailAsync(string normalizedEmail, CancellationToken ct = default);

    /// <summary>
    /// Like <see cref="FindByEmailAsync"/> but also returns Google-linked accounts (those the
    /// password locator skips). Used only by the password-reset request, so a user who signed
    /// up with Google can still set/reset a local password by e-mail.
    /// </summary>
    Task<IReadOnlyList<AccountLocation>> FindAnyByEmailAsync(string normalizedEmail, CancellationToken ct = default);

    /// <summary>Whether the office is still disabled (a fresh self-serve signup awaiting a platform admin).</summary>
    Task<bool> IsOfficeDisabledAsync(Guid officeId, CancellationToken ct = default);
}

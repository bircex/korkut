using Korkut.Domain.Auth;

namespace Korkut.Application.Auth;

/// <summary>
/// Finds which office(s) an e-mail has an account in, before any session exists. Sign-in
/// cannot be tenant-scoped until this answers, so the adapter is the one sanctioned
/// RLS bypass for the flow.
/// </summary>
public interface IAccountLocator
{
    /// <summary>Every account with this e-mail, across offices. Empty when there is none.</summary>
    Task<IReadOnlyList<AccountLocation>> FindByEmailAsync(string normalizedEmail, CancellationToken ct = default);
}

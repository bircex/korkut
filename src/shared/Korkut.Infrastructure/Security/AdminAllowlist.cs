namespace Korkut.Infrastructure.Security;

/// <summary>
/// The platform-admin email allowlist — the SINGLE source of truth, compiled into
/// the application. There is deliberately NO env/DB/UI override, and the stale
/// <c>KORKUT_SUPERADMIN_EMAILS</c> variable is never read (KDM-020). A Google-verified
/// email that is not on this list can never obtain an admin session.
/// </summary>
public static class AdminAllowlist
{
    // Edit here (and only here) to change platform admins. Normalized comparison.
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "recepkizilarslan@gmail.com",
    };

    /// <summary>Exact normalization used for both storage and comparison (trim + lower-invariant).</summary>
    public static string Normalize(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsAllowed(string email) => Allowed.Contains(Normalize(email));

    public static int Count => Allowed.Count;
}

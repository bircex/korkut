namespace Korkut.Organization.Services;

/// <summary>
/// Blocks self-serve registration from known disposable / throwaway ("temp mail") e-mail providers,
/// so a signup has to use a real inbox. Owner-initiated flows (invites) are trusted and not checked.
/// The list is a pragmatic denylist of common providers; extend as needed.
/// </summary>
public static class DisposableEmailPolicy
{
    private static readonly HashSet<string> Blocked = new(StringComparer.OrdinalIgnoreCase)
    {
        "mailinator.com", "guerrillamail.com", "guerrillamailblock.com", "sharklasers.com",
        "grr.la", "10minutemail.com", "10minutemail.net", "tempmail.com", "temp-mail.org",
        "tempmail.dev", "tempmailo.com", "throwawaymail.com", "yopmail.com", "yopmail.net",
        "getnada.com", "nada.email", "trashmail.com", "trashmail.de", "maildrop.cc",
        "mailnesia.com", "fakeinbox.com", "dispostable.com", "mintemail.com", "mailcatch.com",
        "tempinbox.com", "mohmal.com", "spamgourmet.com", "moakt.com", "emailondeck.com",
        "mytemp.email", "dropmail.me", "burnermail.io", "getairmail.com", "inboxbear.com",
        "tempr.email", "spam4.me", "harakirimail.com", "mailde.info", "1secmail.com",
        "1secmail.org", "1secmail.net", "email-temp.com", "gufum.com", "sandouxian.com",
    };

    /// <summary>True when the e-mail's domain is a known disposable provider.</summary>
    public static bool IsDisposable(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var at = email.LastIndexOf('@');
        if (at < 0 || at == email.Length - 1)
        {
            return false;
        }

        return Blocked.Contains(email[(at + 1)..].Trim());
    }
}

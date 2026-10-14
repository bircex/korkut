using Korkut.Contract.Organization;
using Korkut.Contract.Settings;
using Korkut.Settings.Repositories;

namespace Korkut.Settings.Services;

/// <summary>The office's notification address, from its mail preference with the owner as fallback.</summary>
internal sealed class NotificationAddressLookup(IMailPreferenceRepository preferences, IUserLookup users) : INotificationAddressLookup
{
    public async Task<string?> AddressAsync(Guid officeId, CancellationToken ct = default)
    {
        var preference = await preferences.FindAsync(officeId, ct);
        if (preference is { NotificationsEnabled: false })
        {
            return null;
        }

        return preference?.NotifyEmail ?? await users.OwnerEmailAsync(officeId, ct);
    }
}

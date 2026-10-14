using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Settings;

/// <summary>The office mail preference, with whether this caller may change it.</summary>
public sealed record MailPreferenceResponse(bool NotificationsEnabled, string? NotifyEmail, bool CanEdit);

/// <summary>Body for setting the office mail preference.</summary>
public sealed record SaveMailPreferenceRequest
{
    public bool NotificationsEnabled { get; init; } = true;

    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    [MaxLength(320, ErrorMessage = "E-posta adresi en fazla 320 karakter olabilir.")]
    public string? NotifyEmail { get; init; }
}

/// <summary>Office mail-preference use-cases (cross-module contract). Calls RETURN a <see cref="Result"/>.</summary>
public interface IMailPreferenceService
{
    Task<Result<MailPreferenceResponse>> GetAsync(CancellationToken ct = default);

    Task<Result<MailPreferenceResponse>> SaveAsync(SaveMailPreferenceRequest request, CancellationToken ct = default);
}

/// <summary>
/// Where the office's notification mail goes: the address set in Ayarlar → Bildirimler, else the
/// owner's. Null when the office turned notification mail off. Reads in the caller's tenant scope.
/// </summary>
public interface INotificationAddressLookup
{
    Task<string?> AddressAsync(Guid officeId, CancellationToken ct = default);
}

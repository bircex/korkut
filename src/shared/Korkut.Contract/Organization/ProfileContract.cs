using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Organization;

/// <summary>
/// The signed-in user as the client's session probe expects it (Username repeats the e-mail).
/// <paramref name="HasPassword"/> and <paramref name="GoogleLinked"/> say which sign-in methods the
/// account can use; either or both may be true.
/// </summary>
public sealed record ProfileResponse(
    string Username, string Email, string? FullName, string? BarRegistryNo, string? Office, string Role,
    bool HasPassword, bool GoogleLinked);

/// <summary>Body for the caller updating their own profile.</summary>
public sealed record UpdateProfileRequest
{
    [MaxLength(200, ErrorMessage = "Ad en fazla 200 karakter olabilir.")]
    public string? FullName { get; init; }

    [MaxLength(50, ErrorMessage = "Baro sicil no en fazla 50 karakter olabilir.")]
    public string? BarRegistryNo { get; init; }
}

/// <summary>The caller's own account (cross-module contract). Never takes a user id. Calls RETURN a <see cref="Result"/>.</summary>
public interface IProfileService
{
    Task<Result<ProfileResponse>> GetAsync(CancellationToken ct = default);

    Task<Result<ProfileResponse>> UpdateAsync(UpdateProfileRequest request, CancellationToken ct = default);
}

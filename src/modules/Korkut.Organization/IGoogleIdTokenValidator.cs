using Korkut.Organization.Entities;
namespace Korkut.Organization;

/// <summary>Validates a Google ID token's signature and standard claims, or throws.</summary>
public interface IGoogleIdTokenValidator
{
    Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken ct = default);
}

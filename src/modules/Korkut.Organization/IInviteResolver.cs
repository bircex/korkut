
namespace Korkut.Organization;

/// <summary>
/// Resolves what an opaque invite token grants before any session exists, so the accept
/// flow can find the office it belongs to. Null for an unknown, expired or already-accepted
/// invite — the caller must not be able to tell those apart.
/// </summary>
public interface IInviteResolver
{
    Task<InviteGrant?> ResolveAsync(string tokenHash, CancellationToken ct = default);
}

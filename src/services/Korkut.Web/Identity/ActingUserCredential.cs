using Korkut.Core;
using Korkut.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Web.Identity;

/// <summary>
/// Mints an ordinary access token for the acting member, so an internal call made on their behalf
/// is checked exactly like one made from their browser session. The token lives 15 minutes.
/// </summary>
public sealed class ActingUserCredential(IServiceProvider services) : IActingUserCredential
{
    public string? BearerToken()
    {
        // Resolved lazily: the host registers the token service after the domain.
        if (ActingUser.Ambient is not { } member || services.GetService<AccessTokenService>() is not { } tokens)
        {
            return null;
        }

        return tokens.Mint(new AccessClaims(member.UserId, member.OfficeId, member.Role, "acting-user"));
    }
}

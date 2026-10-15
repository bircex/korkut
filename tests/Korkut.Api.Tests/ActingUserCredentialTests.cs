using Korkut.Core;
using Korkut.Security;
using Korkut.Web.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Api.Tests;

/// <summary>
/// Work run as a member outside a request (a Telegram message) gets an ordinary access token for
/// that member when it calls an internal service, and only within the flow that set the member.
/// </summary>
public class ActingUserCredentialTests
{
    private static readonly AccessTokenService Tokens = new("acting-user-credential-test-secret-32-bytes+", SystemClock.Instance);

    private static ActingUserCredential Credential()
        => new ActingUserCredential(new ServiceCollection().AddSingleton(Tokens).BuildServiceProvider());

    [Fact]
    public async Task A_flow_acting_as_a_member_gets_a_token_for_exactly_that_member()
    {
        var (office, user) = (Guid.NewGuid(), Guid.NewGuid());
        var credential = Credential();

        var token = await Task.Run(() =>
        {
            new ActingUser().Set(office, user, Roles.Lawyer, new HashSet<string> { Permissions.MattersRead });
            return credential.BearerToken();
        });

        var claims = await Tokens.ValidateAsync(token!);
        Assert.Equal(new AccessClaims(user, office, Roles.Lawyer, "acting-user"), claims);
    }

    [Fact]
    public async Task Outside_such_a_flow_there_is_no_token()
    {
        var credential = Credential();
        await Task.Run(() => new ActingUser().Set(Guid.NewGuid(), Guid.NewGuid(), Roles.Lawyer, new HashSet<string>()));

        // The member was set in another flow; it never leaks back here.
        Assert.Null(credential.BearerToken());
    }
}

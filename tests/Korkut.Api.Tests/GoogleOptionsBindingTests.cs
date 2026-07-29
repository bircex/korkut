using Korkut.Security;
using Microsoft.Extensions.Configuration;

namespace Korkut.Api.Tests;

/// <summary>The existing Google env names must bind verbatim (KDM-016 / KDM-033).</summary>
public class GoogleOptionsBindingTests
{
    [Fact]
    public void Binds_the_exact_google_env_names()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GOOGLE_CLIENT_ID"] = "cid",
            ["GOOGLE_CLIENT_SECRET"] = "csecret",
            ["GOOGLE_REDIRECT_URI"] = "https://app/callback",
        }).Build();

        var options = SecuritySetup.BindGoogleOptions(config);

        Assert.Equal("cid", options.ClientId);
        Assert.Equal("https://app/callback", options.RedirectUri);
        Assert.True(options.IsConfigured);
    }

    [Fact]
    public void Is_not_configured_when_env_missing()
    {
        var options = SecuritySetup.BindGoogleOptions(new ConfigurationBuilder().Build());
        Assert.False(options.IsConfigured);
    }
}

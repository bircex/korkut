using System.Net;
using Korkut.Web;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Korkut.Api.Tests;

public class ClientAddressTests
{
    private static DefaultHttpContext Request(string? cfHeader)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.7");
        if (cfHeader is not null)
        {
            context.Request.Headers["CF-Connecting-IP"] = cfHeader;
        }

        return context;
    }

    [Fact]
    public void Forwarded_header_is_ignored_unless_the_operator_names_it()
    {
        // A caller that is not behind Cloudflare must not choose its own rate-limit bucket.
        Assert.Equal("10.0.0.7", ClientAddress.Resolve(Request("203.0.113.9"), trustedHeader: null));
    }

    [Fact]
    public void Named_header_wins_when_present()
    {
        Assert.Equal("203.0.113.9", ClientAddress.Resolve(Request("203.0.113.9"), "CF-Connecting-IP"));
    }

    [Fact]
    public void Named_header_absent_falls_back_to_the_socket_address()
    {
        Assert.Equal("10.0.0.7", ClientAddress.Resolve(Request(null), "CF-Connecting-IP"));
    }
}

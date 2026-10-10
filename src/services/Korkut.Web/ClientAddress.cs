using Microsoft.AspNetCore.Http;

namespace Korkut.Web;

/// <summary>
/// Resolves the client address that per-client limits partition on. A forwarded-client header
/// is trusted only when the operator names one (<c>KORKUT_CLIENT_IP_HEADER</c>), because any
/// caller can send any header: trusting e.g. <c>CF-Connecting-IP</c> on an install that is not
/// behind Cloudflare lets every request pick its own bucket and walk past the limit. Name a
/// header only when a proxy you control overwrites it on every request.
/// </summary>
public static class ClientAddress
{
    public const string HeaderSetting = "KORKUT_CLIENT_IP_HEADER";

    public static string Resolve(HttpContext context, string? trustedHeader)
    {
        if (!string.IsNullOrWhiteSpace(trustedHeader)
            && context.Request.Headers[trustedHeader].FirstOrDefault() is { Length: > 0 } forwarded)
        {
            return forwarded.Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

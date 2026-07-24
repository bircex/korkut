using Korkut.Core;
using Korkut.Web.Tenancy;
using Microsoft.AspNetCore.Http;

namespace Korkut.Web.Identity;

/// <summary>
/// <see cref="ICurrentUser"/> backed by the request's <c>ClaimsPrincipal</c> (the trusted
/// access token). Office, user and permissions come only from token claims.
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal User =>
        accessor.HttpContext?.User ?? throw new InvalidOperationException("No HTTP context on this request.");

    public Guid OfficeId => User.OfficeId();

    public Guid UserId => User.UserId();

    public IReadOnlySet<string> Permissions => User.Permissions();
}

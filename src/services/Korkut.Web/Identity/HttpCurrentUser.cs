using Korkut.Core;
using Korkut.Web.Tenancy;
using Microsoft.AspNetCore.Http;

namespace Korkut.Web.Identity;

/// <summary>
/// <see cref="ICurrentUser"/> backed by the request's <c>ClaimsPrincipal</c> (the trusted
/// access token). Office, user and permissions come only from token claims, unless the scope
/// runs as a member outside any request (<see cref="ActingUser"/>).
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor, ActingUser acting) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal User =>
        accessor.HttpContext?.User ?? throw new InvalidOperationException("No HTTP context on this request.");

    public Guid OfficeId => acting.Current?.OfficeId ?? User.OfficeId();

    public Guid UserId => acting.Current?.UserId ?? User.UserId();

    public IReadOnlySet<string> Permissions => acting.Current?.Permissions ?? User.Permissions();

    public bool IsKnown => acting.Current is not null || accessor.HttpContext?.User.Identity?.IsAuthenticated == true;
}

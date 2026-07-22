using Korkut.Application.Agent.Abstractions;
using Korkut.Agent.Services.Core;
using Korkut.Web.Tenancy;

namespace Korkut.Agent.Identity;

/// <summary>
/// <see cref="ICurrentUser"/> backed by the request's <c>ClaimsPrincipal</c> (the trusted access
/// token). The office / user / permissions come only from the token, never a DTO or query param.
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal User =>
        accessor.HttpContext?.User ?? throw new InvalidOperationException("No HTTP context on this request.");

    public Guid OfficeId => User.OfficeId();

    public Guid UserId => User.UserId();

    public IReadOnlySet<string> Permissions => User.Permissions();
}

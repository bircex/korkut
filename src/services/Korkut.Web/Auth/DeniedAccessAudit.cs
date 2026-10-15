using Korkut.Contract.Audit;
using Korkut.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Korkut.Web.Auth;

/// <summary>
/// Records every request a signed-in member was refused for lack of a permission (the 403s) in
/// the audit trail: who, which endpoint and which permission it needed. The answer itself is
/// unchanged. A failure to record is logged and never turns into a failed request.
/// </summary>
public sealed partial class DeniedAccessAudit(ILogger<DeniedAccessAudit> logger) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && context.User.Identity?.IsAuthenticated == true)
        {
            await RecordAsync(context, authorizeResult);
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }

    private async Task RecordAsync(HttpContext context, PolicyAuthorizationResult result)
    {
        try
        {
            var services = context.RequestServices;
            var current = services.GetRequiredService<ICurrentUser>();
            var audit = services.GetRequiredService<IAuditLog>();
            var needed = result.AuthorizationFailure?.FailedRequirements
                .OfType<ClaimsAuthorizationRequirement>()
                .SelectMany(r => r.AllowedValues ?? [])
                .ToList();
            var office = current.OfficeId;
            await services.GetRequiredService<IUnitOfWork>().InTenantScopeAsync(office, async () =>
            {
                await audit.WriteAsync(new AuditEntry(
                    office, current.UserId, null, context.TraceIdentifier, AuditActions.AccessDenied, "endpoint", null, null,
                    new { method = context.Request.Method, path = context.Request.Path.Value, permission = needed }),
                    context.RequestAborted);
                return 0;
            }, context.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            NotRecorded(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A refused request could not be written to the audit trail.")]
    private static partial void NotRecorded(ILogger logger, Exception ex);
}

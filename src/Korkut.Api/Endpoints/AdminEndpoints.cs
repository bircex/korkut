using Korkut.Api.Auth;
using Korkut.Api.Errors;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Api.Endpoints;

public sealed record AdminOfficeResponse(Guid Id, string Name, bool Disabled, bool RequireTwoFactor, DateTimeOffset CreatedAt);

public sealed record AdminStatsResponse(int Offices, int ActiveOffices, int DisabledOffices);

/// <summary>
/// Platform-admin office management (KDM-020). A SEPARATE audience — office tokens are
/// rejected by the admin scheme. Cross-tenant by design (offices is the tenant root,
/// not RLS-forced), so these run without a tenant scope. Every route requires the
/// admin scheme and is rate-limited.
/// </summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin")
            .RequireAuthorization(AdminAuthHandler.SchemeName)
            .RequireRateLimiting(Setup.ApiSetup.SensitivePolicy);

        admin.MapGet("/offices", async (KorkutDbContext db, CancellationToken ct) =>
        {
            var offices = await db.Offices.AsNoTracking().OrderBy(x => x.Name)
                .Select(o => new AdminOfficeResponse(o.Id, o.Name, o.Disabled, o.RequireTwoFactor, o.CreatedAt))
                .ToListAsync(ct);
            return Results.Ok(offices);
        });

        admin.MapPost("/offices/{id:guid}/enable", (Guid id, KorkutDbContext db, CancellationToken ct) => SetDisabled(db, id, false, ct));
        admin.MapPost("/offices/{id:guid}/disable", (Guid id, KorkutDbContext db, CancellationToken ct) => SetDisabled(db, id, true, ct));

        admin.MapGet("/stats", async (KorkutDbContext db, CancellationToken ct) =>
        {
            var total = await db.Offices.AsNoTracking().CountAsync(ct);
            var disabled = await db.Offices.AsNoTracking().CountAsync(x => x.Disabled, ct);
            return Results.Ok(new AdminStatsResponse(total, total - disabled, disabled));
        });

        return app;
    }

    private static async Task<IResult> SetDisabled(KorkutDbContext db, Guid officeId, bool disabled, CancellationToken ct)
    {
        var affected = await db.Offices.Where(x => x.Id == officeId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Disabled, disabled), ct);
        return affected > 0 ? Results.NoContent() : throw ApiException.NotFound("Ofis bulunamadı.");
    }
}

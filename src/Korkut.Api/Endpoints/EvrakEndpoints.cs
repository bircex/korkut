using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Korkut.Api.Errors;
using Korkut.Api.Tenancy;
using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;

namespace Korkut.Api.Endpoints;

public sealed record EvrakResponse(
    Guid Id, Guid? MatterId, string Direction, string Title, string? ReferenceNo, DateOnly? DocDate, string? Notes, DateTimeOffset CreatedAt);

public sealed record EvrakCreateRequest
{
    [Required, RegularExpression("incoming|outgoing|internal", ErrorMessage = "direction 'incoming', 'outgoing' veya 'internal' olmalıdır.")]
    public string? Direction { get; init; }

    [Required, MaxLength(500)]
    public string? Title { get; init; }

    [MaxLength(100)]
    public string? ReferenceNo { get; init; }

    public DateOnly? DocDate { get; init; }

    public Guid? MatterId { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }
}

/// <summary>
/// Correspondence-register ("evrak defteri") endpoints on the .NET runtime.
/// Authenticated, tenant-scoped (RLS), DTO-mapped. Reads use matters.read, writes
/// matters.write (evrak is matter-adjacent case work).
/// </summary>
public static class EvrakEndpoints
{
    public static IEndpointRouteBuilder MapEvrakEndpoints(this IEndpointRouteBuilder app)
    {
        var evrak = app.MapGroup("/api/evrak");

        evrak.MapGet("", async (string? direction, ClaimsPrincipal user, KorkutDbContext db, CorrespondenceStore store, CancellationToken ct) =>
            Results.Ok(await user.InTenantScopeAsync(db, async o => (await store.ListAsync(o, direction, ct)).Select(ToDto).ToList(), ct)))
            .RequireAuthorization(Permissions.MattersRead);

        evrak.MapPost("", async (EvrakCreateRequest body, ClaimsPrincipal user, KorkutDbContext db, CorrespondenceStore store, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var created = await user.InTenantScopeAsync(db, async o =>
                {
                    var id = await store.CreateAsync(o, body.Direction!, body.Title!.Trim(), body.ReferenceNo?.Trim(), body.DocDate, body.MatterId, body.Notes?.Trim(), ct);
                    return await store.GetAsync(o, id, ct);
                }, ct);
                return Results.Created($"/api/evrak/{created!.Id}", ToDto(created));
            })
            .RequireAuthorization(Permissions.MattersWrite);

        evrak.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, CorrespondenceStore store, CancellationToken ct) =>
            {
                var ok = await user.InTenantScopeAsync(db, o => store.DeleteAsync(o, id, ct), ct);
                return ok ? Results.NoContent() : throw ApiException.NotFound("Evrak kaydı bulunamadı.");
            })
            .RequireAuthorization(Permissions.MattersWrite);

        return app;
    }

    private static EvrakResponse ToDto(CorrespondenceEntity c) =>
        new(c.Id, c.MatterId, c.Direction, c.Title, c.ReferenceNo, c.DocDate, c.Notes, c.CreatedAt);
}

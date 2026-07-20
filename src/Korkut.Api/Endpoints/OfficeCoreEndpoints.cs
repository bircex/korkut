using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Korkut.Api.Errors;
using Korkut.Api.Tenancy;
using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;

namespace Korkut.Api.Endpoints;

// Transport DTOs (never domain entities). Snake_case is applied by the global JSON
// contract, so `ClientId` serializes as `client_id` to match the Python/UI contract.

public sealed record ClientResponse(Guid Id, string Name, string Kind, string? Email, string? Phone, DateTimeOffset CreatedAt);

public sealed record ClientWriteRequest
{
    [Required(ErrorMessage = "Ad zorunludur.")]
    [MaxLength(300, ErrorMessage = "Ad en fazla 300 karakter olabilir.")]
    public string? Name { get; init; }

    [RegularExpression("individual|corporate", ErrorMessage = "Tür 'individual' veya 'corporate' olmalıdır.")]
    public string Kind { get; init; } = "individual";

    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    public string? Email { get; init; }

    [MaxLength(50)]
    public string? Phone { get; init; }
}

public sealed record MatterResponse(Guid Id, string Title, Guid? ClientId, string Status, string? CourtNameSnapshot, DateTimeOffset CreatedAt);

public sealed record MatterCreateRequest
{
    [Required(ErrorMessage = "Başlık zorunludur.")]
    [MaxLength(500, ErrorMessage = "Başlık en fazla 500 karakter olabilir.")]
    public string? Title { get; init; }

    public Guid? ClientId { get; init; }

    public Guid? CourtId { get; init; }
}

/// <summary>
/// Office-core CRUD (clients, matters) on the .NET runtime. Every route: authenticated
/// (deny-by-default permission), tenant-scoped via the token's office claim so RLS
/// applies, and maps DTOs — never entities. Paths match the frozen UI/Python contract.
/// </summary>
public static class OfficeCoreEndpoints
{
    public static IEndpointRouteBuilder MapOfficeCoreEndpoints(this IEndpointRouteBuilder app)
    {
        MapClients(app);
        MapMatters(app);
        return app;
    }

    private static void MapClients(IEndpointRouteBuilder app)
    {
        var clients = app.MapGroup("/api/clients");

        clients.MapGet("", async (ClaimsPrincipal user, KorkutDbContext db, OfficeCoreStore store, CancellationToken ct) =>
            Results.Ok(await user.InTenantScopeAsync(db,
                async office => (await store.ListClientsAsync(office, ct)).Select(ToDto).ToList(), ct)))
            .RequireAuthorization(Permissions.ClientsRead);

        clients.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, OfficeCoreStore store, CancellationToken ct) =>
            {
                var client = await user.InTenantScopeAsync(db, office => store.GetClientAsync(office, id, ct), ct);
                return client is null ? throw ApiException.NotFound("Müvekkil bulunamadı.") : Results.Ok(ToDto(client));
            })
            .RequireAuthorization(Permissions.ClientsRead);

        clients.MapPost("", async (ClientWriteRequest body, ClaimsPrincipal user, KorkutDbContext db, OfficeCoreStore store, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var created = await user.InTenantScopeAsync(db, async office =>
                {
                    var id = await store.CreateClientAsync(office, body.Name!.Trim(), body.Kind, body.Email, body.Phone, ct);
                    return await store.GetClientAsync(office, id, ct);
                }, ct);
                return Results.Created($"/api/clients/{created!.Id}", ToDto(created));
            })
            .RequireAuthorization(Permissions.ClientsWrite);

        clients.MapPatch("/{id:guid}", async (Guid id, ClientWriteRequest body, ClaimsPrincipal user, KorkutDbContext db, OfficeCoreStore store, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var updated = await user.InTenantScopeAsync(db, async office =>
                {
                    var ok = await store.UpdateClientAsync(office, id, body.Name!.Trim(), body.Kind, body.Email, body.Phone, ct);
                    return ok ? await store.GetClientAsync(office, id, ct) : null;
                }, ct);
                return updated is null ? throw ApiException.NotFound("Müvekkil bulunamadı.") : Results.Ok(ToDto(updated));
            })
            .RequireAuthorization(Permissions.ClientsWrite);

        clients.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, OfficeCoreStore store, CancellationToken ct) =>
            {
                var ok = await user.InTenantScopeAsync(db, office => store.SoftDeleteClientAsync(office, id, ct), ct);
                return ok ? Results.NoContent() : throw ApiException.NotFound("Müvekkil bulunamadı.");
            })
            .RequireAuthorization(Permissions.ClientsWrite);
    }

    private static void MapMatters(IEndpointRouteBuilder app)
    {
        var matters = app.MapGroup("/api/matters");

        matters.MapGet("", async (ClaimsPrincipal user, KorkutDbContext db, OfficeCoreStore store, CancellationToken ct) =>
            Results.Ok(await user.InTenantScopeAsync(db,
                async office => (await store.ListMattersAsync(office, ct)).Select(ToDto).ToList(), ct)))
            .RequireAuthorization(Permissions.MattersRead);

        matters.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, OfficeCoreStore store, CancellationToken ct) =>
            {
                var matter = await user.InTenantScopeAsync(db, office => store.GetMatterAsync(office, id, ct), ct);
                return matter is null ? throw ApiException.NotFound("Dosya bulunamadı.") : Results.Ok(ToDto(matter));
            })
            .RequireAuthorization(Permissions.MattersRead);

        matters.MapPost("", async (MatterCreateRequest body, ClaimsPrincipal user, KorkutDbContext db, OfficeCoreStore store, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var created = await user.InTenantScopeAsync(db, async office =>
                {
                    var id = await store.CreateMatterAsync(office, body.Title!.Trim(), body.ClientId, body.CourtId, ct);
                    return await store.GetMatterAsync(office, id, ct);
                }, ct);
                return Results.Created($"/api/matters/{created!.Id}", ToDto(created));
            })
            .RequireAuthorization(Permissions.MattersWrite);

        matters.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, OfficeCoreStore store, CancellationToken ct) =>
            {
                await user.InTenantScopeAsync(db, async office =>
                {
                    await store.SoftDeleteMatterAsync(office, id, ct);
                    return true;
                }, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(Permissions.MattersDelete);
    }

    private static ClientResponse ToDto(ClientEntity c) => new(c.Id, c.Name, c.Kind, c.Email, c.Phone, c.CreatedAt);

    private static MatterResponse ToDto(MatterEntity m) => new(m.Id, m.Title, m.ClientId, m.Status, m.CourtNameSnapshot, m.CreatedAt);
}

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Korkut.Api.Errors;
using Korkut.Api.Tenancy;
using Korkut.Application.Features.Settings;
using Korkut.Application.Security;
using Korkut.Domain.Settings;
using Korkut.Infrastructure.Persistence;

namespace Korkut.Api.Endpoints;

// --- Persona ---
public sealed record PersonaResponse(string Tone, string Expertise, string ExtraRules, bool CanEdit);

public sealed record PersonaInput
{
    [MaxLength(2000)]
    public string Tone { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string Expertise { get; init; } = string.Empty;

    [MaxLength(4000)]
    public string ExtraRules { get; init; } = string.Empty;
}

// --- Style ---
public sealed record StyleFieldsDto(
    [property: MaxLength(2000)] string? Letterhead,
    [property: MaxLength(2000)] string? Signature,
    [property: MaxLength(2000)] string? Tone,
    [property: MaxLength(4000)] string? Boilerplate);

public sealed record StyleResponse(StyleFieldsDto Office, StyleFieldsDto User, StyleFieldsDto Effective, bool CanEditOffice);

// --- Memory ---
public sealed record MemoryItemResponse(Guid Id, string Kind, string Text, string Status, string? Author, DateTimeOffset CreatedAt);

public sealed record MemoryListResponse(IReadOnlyList<MemoryItemResponse> Items);

public sealed record MemoryCreateRequest
{
    [Required, MaxLength(4000)]
    public string? Text { get; init; }

    [MaxLength(50)]
    public string Kind { get; init; } = "note";
}

/// <summary>
/// Office-settings endpoints: the AI persona, drafting style (office + user layers,
/// with an effective overlay), and the office memory list. Authenticated, tenant-scoped
/// (RLS). Reads are open to case-workers (matters.read); office-wide writes and memory
/// curation require office.settings; a user edits their own style with matters.read.
/// </summary>
public static class SettingsEndpoints
{
    private const string OfficeScope = "office";
    private const string UserScope = "user";

    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        MapPersona(app);
        MapStyle(app);
        MapMemory(app);
        return app;
    }

    private static void MapPersona(IEndpointRouteBuilder app)
    {
        var persona = app.MapGroup("/api/persona");

        persona.MapGet("", async (ClaimsPrincipal user, KorkutDbContext db, IOfficeSettingsService svc, CancellationToken ct) =>
            {
                var canEdit = user.HasPermission(Permissions.OfficeSettings);
                var p = await user.InTenantScopeAsync(db, o => svc.GetPersonaAsync(o, ct), ct);
                return Results.Ok(new PersonaResponse(p?.Tone ?? "", p?.Expertise ?? "", p?.ExtraRules ?? "", canEdit));
            })
            .RequireAuthorization(Permissions.MattersRead);

        persona.MapPut("", async (PersonaInput body, ClaimsPrincipal user, KorkutDbContext db, IOfficeSettingsService svc, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var p = await user.InTenantScopeAsync(db, o => svc.UpsertPersonaAsync(o, body.Tone, body.Expertise, body.ExtraRules, ct), ct);
                return Results.Ok(new PersonaResponse(p.Tone, p.Expertise, p.ExtraRules, true));
            })
            .RequireAuthorization(Permissions.OfficeSettings);
    }

    private static void MapStyle(IEndpointRouteBuilder app)
    {
        var style = app.MapGroup("/api/style");

        style.MapGet("", async (ClaimsPrincipal user, KorkutDbContext db, IOfficeSettingsService svc, CancellationToken ct) =>
            {
                var uid = user.UserId();
                var canEditOffice = user.HasPermission(Permissions.OfficeSettings);
                var (office, self) = await user.InTenantScopeAsync(db, async o =>
                    (await svc.GetStyleAsync(o, OfficeScope, Guid.Empty, ct), await svc.GetStyleAsync(o, UserScope, uid, ct)), ct);
                var effective = self.Over(office);
                return Results.Ok(new StyleResponse(ToDto(office), ToDto(self), ToDto(effective), canEditOffice));
            })
            .RequireAuthorization(Permissions.MattersRead);

        // A user edits their OWN style (any case-worker) — writes are scoped to their user id.
        style.MapPut("/user", async (StyleFieldsDto body, ClaimsPrincipal user, KorkutDbContext db, IOfficeSettingsService svc, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var uid = user.UserId();
                var saved = await user.InTenantScopeAsync(db, o => svc.SaveStyleAsync(o, UserScope, uid, ToFields(body), ct), ct);
                return Results.Ok(ToDto(saved));
            })
            .RequireAuthorization(Permissions.MattersRead);

        style.MapPut("/office", async (StyleFieldsDto body, ClaimsPrincipal user, KorkutDbContext db, IOfficeSettingsService svc, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var saved = await user.InTenantScopeAsync(db, o => svc.SaveStyleAsync(o, OfficeScope, Guid.Empty, ToFields(body), ct), ct);
                return Results.Ok(ToDto(saved));
            })
            .RequireAuthorization(Permissions.OfficeSettings);
    }

    private static void MapMemory(IEndpointRouteBuilder app)
    {
        var memory = app.MapGroup("/api/memory");

        memory.MapGet("", async (ClaimsPrincipal user, KorkutDbContext db, IOfficeSettingsService svc, CancellationToken ct) =>
            {
                var items = await user.InTenantScopeAsync(db, async o => (await svc.ListMemoryAsync(o, ct)).Select(ToDto).ToList(), ct);
                return Results.Ok(new MemoryListResponse(items));
            })
            .RequireAuthorization(Permissions.MattersRead);

        // Any case-worker can propose a memory; owners' items land active, members' pending.
        memory.MapPost("", async (MemoryCreateRequest body, ClaimsPrincipal user, KorkutDbContext db, IOfficeSettingsService svc, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var uid = user.UserId();
                var active = user.HasPermission(Permissions.OfficeSettings);
                var created = await user.InTenantScopeAsync(db, o => svc.AddMemoryAsync(o, body.Text!.Trim(), body.Kind, uid, active, ct), ct);
                return Results.Created($"/api/memory/{created.Id}", ToDto(created));
            })
            .RequireAuthorization(Permissions.MattersWrite);

        memory.MapPost("/{id:guid}/approve", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, IOfficeSettingsService svc, CancellationToken ct) =>
            {
                var approved = await user.InTenantScopeAsync(db, o => svc.ApproveMemoryAsync(o, id, ct), ct);
                return approved is null ? throw ApiException.NotFound("Not bulunamadı.") : Results.Ok(ToDto(approved));
            })
            .RequireAuthorization(Permissions.OfficeSettings);

        memory.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, IOfficeSettingsService svc, CancellationToken ct) =>
            {
                var ok = await user.InTenantScopeAsync(db, o => svc.DeleteMemoryAsync(o, id, ct), ct);
                return ok ? Results.NoContent() : throw ApiException.NotFound("Not bulunamadı.");
            })
            .RequireAuthorization(Permissions.OfficeSettings);
    }

    private static StyleFieldsDto ToDto(StyleFields f) => new(f.Letterhead, f.Signature, f.Tone, f.Boilerplate);

    private static StyleFields ToFields(StyleFieldsDto d) =>
        new(d.Letterhead ?? "", d.Signature ?? "", d.Tone ?? "", d.Boilerplate ?? "");

    private static MemoryItemResponse ToDto(MemoryItem m) => new(m.Id, m.Kind, m.Text, m.Status, m.Author, m.CreatedAt);
}

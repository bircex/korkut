using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Korkut.Web.Errors;
using Korkut.Web.Tenancy;
using Korkut.Application.Features.Templates;
using Korkut.Application.Security;
using Korkut.Domain.Templates;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Templates;

namespace Korkut.Api.Endpoints;

// --- Petition types ---
public sealed record PetitionTypeResponse(string Id, string Ad, string? Mahkeme, bool Sik);

public sealed record PetitionCategoryResponse(string Kategori, IReadOnlyList<PetitionTypeResponse> Dilekceler);

// --- Templates ---
public sealed record TemplateTypeRow(string Id, string Ad, string? Ref, bool HasOfficeOverride, bool HasUserOverride);

public sealed record TemplateCategoryResponse(string Kategori, IReadOnlyList<TemplateTypeRow> Tipler);

public sealed record TemplateListResponse(string Kind, IReadOnlyList<TemplateCategoryResponse> Kategoriler);

public sealed record TemplateDetailResponse(
    string Id, string Ad, string Kind, string? Default, string? OfficeOverride, string? UserOverride, string? Effective);

public sealed record TemplateSaveRequest
{
    [Required, RegularExpression("user|office")]
    public string? Scope { get; init; }

    [Required]
    public string? Content { get; init; }
}

/// <summary>
/// Document template catalog + per-office/user overrides, and the petition-type picker.
/// Authenticated, tenant-scoped (RLS). The built-in catalog is static reference data;
/// overrides are stored per office/user. Reads use matters.read; a user saves/deletes
/// their own override with matters.read, office-wide overrides require office.settings.
/// </summary>
public static class TemplateEndpoints
{
    public static IEndpointRouteBuilder MapTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/petition-types", (TemplateCatalog catalog) =>
            {
                var categories = catalog.Categories(TemplateCatalog.Dilekce)
                    .Select(c => new PetitionCategoryResponse(c.Kategori,
                        c.Types.Select(t => new PetitionTypeResponse(t.Id, t.Ad, t.Ref, t.Sik)).ToList()))
                    .ToList();
                return Results.Ok(categories);
            })
            .RequireAuthorization(Permissions.MattersRead);

        var templates = app.MapGroup("/api/templates");

        templates.MapGet("", async (string? kind, ClaimsPrincipal user, KorkutDbContext db, TemplateCatalog catalog, ITemplateOverrideService svc, CancellationToken ct) =>
            {
                var k = kind ?? TemplateCatalog.Dilekce;
                if (!catalog.IsKnownKind(k))
                {
                    throw ApiException.BadRequest("Geçersiz şablon türü.");
                }

                var uid = user.UserId();
                var overrides = await user.InTenantScopeAsync(db, o => svc.ListAsync(o, k, ct), ct);
                var officeIds = overrides.Where(x => x.Scope == TemplateScope.Office).Select(x => x.TypeId).ToHashSet();
                var userIds = overrides.Where(x => x.Scope == TemplateScope.User && x.UserId == uid).Select(x => x.TypeId).ToHashSet();

                var kategoriler = catalog.Categories(k)
                    .Select(c => new TemplateCategoryResponse(c.Kategori,
                        c.Types.Select(t => new TemplateTypeRow(t.Id, t.Ad, t.Ref, officeIds.Contains(t.Id), userIds.Contains(t.Id))).ToList()))
                    .ToList();
                return Results.Ok(new TemplateListResponse(k, kategoriler));
            })
            .RequireAuthorization(Permissions.MattersRead);

        templates.MapGet("/{typeId}", async (string typeId, ClaimsPrincipal user, KorkutDbContext db, TemplateCatalog catalog, ITemplateOverrideService svc, CancellationToken ct) =>
            {
                var (kind, type) = Resolve(catalog, typeId);
                var uid = user.UserId();
                var (officeOverride, userOverride) = await user.InTenantScopeAsync(db, async o =>
                {
                    var office = await svc.GetContentAsync(o, TemplateScope.Office, Guid.Empty, kind, typeId, ct);
                    var self = await svc.GetContentAsync(o, TemplateScope.User, uid, kind, typeId, ct);
                    return (office, self);
                }, ct);

                var def = catalog.DefaultContent(kind, typeId);
                var effective = userOverride ?? officeOverride ?? def;
                return Results.Ok(new TemplateDetailResponse(typeId, type.Ad, kind, def, officeOverride, userOverride, effective));
            })
            .RequireAuthorization(Permissions.MattersRead);

        templates.MapPut("/{typeId}", async (string typeId, TemplateSaveRequest body, ClaimsPrincipal user, KorkutDbContext db, TemplateCatalog catalog, ITemplateOverrideService svc, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var (kind, _) = Resolve(catalog, typeId);
                var office = body.Scope == TemplateScope.Office;
                if (office && !user.HasPermission(Permissions.OfficeSettings))
                {
                    throw ApiException.Forbidden("Ofis şablonunu düzenleme yetkiniz yok.");
                }

                var uid = user.UserId();
                await user.InTenantScopeAsync(db, async o =>
                {
                    await svc.SaveAsync(o, body.Scope!, office ? Guid.Empty : uid, kind, typeId, body.Content!, ct);
                    return 0;
                }, ct);
                return Results.Ok(new { ok = true });
            })
            .RequireAuthorization(Permissions.MattersRead);

        templates.MapDelete("/{typeId}", async (string typeId, string? scope, ClaimsPrincipal user, KorkutDbContext db, TemplateCatalog catalog, ITemplateOverrideService svc, CancellationToken ct) =>
            {
                if (scope is not (TemplateScope.Office or TemplateScope.User))
                {
                    throw ApiException.BadRequest("scope 'user' veya 'office' olmalıdır.");
                }

                var (kind, _) = Resolve(catalog, typeId);
                var office = scope == TemplateScope.Office;
                if (office && !user.HasPermission(Permissions.OfficeSettings))
                {
                    throw ApiException.Forbidden("Ofis şablonunu düzenleme yetkiniz yok.");
                }

                var uid = user.UserId();
                var ok = await user.InTenantScopeAsync(db, o => svc.DeleteAsync(o, scope!, office ? Guid.Empty : uid, kind, typeId, ct), ct);
                return ok ? Results.Ok(new { ok = true }) : throw ApiException.NotFound("Şablon geçersiz kılması bulunamadı.");
            })
            .RequireAuthorization(Permissions.MattersRead);

        return app;
    }

    /// <summary>Resolve a type id to its (kind, type) across both catalogs, or 404.</summary>
    private static (string Kind, CatalogType Type) Resolve(TemplateCatalog catalog, string typeId)
    {
        foreach (var kind in new[] { TemplateCatalog.Dilekce, TemplateCatalog.Sozlesme })
        {
            var type = catalog.FindType(kind, typeId);
            if (type is not null)
            {
                return (kind, type);
            }
        }

        throw ApiException.NotFound("Şablon türü bulunamadı.");
    }
}

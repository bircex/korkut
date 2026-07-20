using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Korkut.Api.Errors;
using Korkut.Api.Tenancy;
using Korkut.Application.Security;
using Korkut.Infrastructure.Documents;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;

namespace Korkut.Api.Endpoints;

public sealed record DocumentResponse(
    Guid Id, string OriginalFilename, string ContentType, long Size, string Status, int Version, DateTimeOffset CreatedAt);

public sealed record PresignResponse(string Url);

public sealed record LinkRequest
{
    [Required, RegularExpression("client|matter|party", ErrorMessage = "target_type 'client', 'matter' veya 'party' olmalıdır.")]
    public string? TargetType { get; init; }

    [Required]
    public Guid TargetId { get; init; }

    public bool IsPrimary { get; init; }
}

/// <summary>
/// Secure-document endpoints (KDM-025..028): quarantine-based upload with magic-byte
/// validation + malware scan, presigned download gated on the clean state, typed
/// linking, and legal-hold-aware delete. Every route is authenticated, deny-by-
/// default, and tenant-scoped so RLS applies.
/// </summary>
public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var docs = app.MapGroup("/api/documents");

        docs.MapGet("", async (Guid? matterId, ClaimsPrincipal user, KorkutDbContext db, DocumentService service, CancellationToken ct) =>
            Results.Ok(await user.InTenantScopeAsync(db,
                async office => (await service.ListAsync(office, matterId, ct)).Select(ToDto).ToList(), ct)))
            .RequireAuthorization(Permissions.DocumentsRead);

        docs.MapPost("", async (IFormFile file, ClaimsPrincipal user, KorkutDbContext db, SecureUploadService upload, DocumentService service, CancellationToken ct) =>
            {
                if (file is null || file.Length == 0)
                {
                    throw ApiException.BadRequest("Dosya gerekli.");
                }

                var (outcome, dto) = await user.InTenantScopeAsync(db, async office =>
                {
                    await using var stream = file.OpenReadStream();
                    var result = await upload.IngestAsync(office, user.UserId(), stream, file.FileName, ct);
                    var doc = result.DocumentId is { } id ? await service.GetAsync(office, id, ct) : null;
                    return (result, doc is null ? null : ToDto(doc));
                }, ct);

                // Validation rejection (bad magic bytes / filename) never stored → 422.
                if (dto is null)
                {
                    return Results.Json(new { detail = $"Yükleme reddedildi: {outcome.Rejection}." }, statusCode: 422);
                }

                return Results.Created($"/api/documents/{dto.Id}", dto);
            })
            .RequireAuthorization(Permissions.DocumentsWrite)
            .DisableAntiforgery();

        docs.MapGet("/{id:guid}/download", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, DocumentService service, CancellationToken ct) =>
            {
                var url = await user.InTenantScopeAsync(db, async office =>
                {
                    var doc = await service.GetAsync(office, id, ct) ?? throw ApiException.NotFound("Belge bulunamadı.");
                    if (doc.Status != "clean")
                    {
                        throw ApiException.Conflict("Belge henüz indirilemez (tarama tamamlanmadı veya reddedildi).");
                    }

                    return await service.PresignAsync(office, id, TimeSpan.FromMinutes(2), ct);
                }, ct);
                return Results.Ok(new PresignResponse(url));
            })
            .RequireAuthorization(Permissions.DocumentsRead);

        docs.MapPost("/{id:guid}/links", async (Guid id, LinkRequest body, ClaimsPrincipal user, KorkutDbContext db, DocumentRelationService relations, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var relId = await user.InTenantScopeAsync(db,
                    office => relations.LinkAsync(office, user.UserId(), id, body.TargetType!, body.TargetId, body.IsPrimary, ct), ct);
                return Results.Ok(new { id = relId });
            })
            .RequireAuthorization(Permissions.DocumentsWrite);

        docs.MapDelete("/{id:guid}/links", async (Guid id, [Microsoft.AspNetCore.Mvc.FromBody] LinkRequest body, ClaimsPrincipal user, KorkutDbContext db, DocumentRelationService relations, CancellationToken ct) =>
            {
                await user.InTenantScopeAsync(db, async office =>
                {
                    await relations.UnlinkAsync(office, user.UserId(), id, body.TargetType!, body.TargetId, ct);
                    return true;
                }, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(Permissions.DocumentsWrite);

        docs.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, DocumentLifecycleService lifecycle, CancellationToken ct) =>
            {
                await user.InTenantScopeAsync(db, async office =>
                {
                    await lifecycle.TombstoneAsync(office, id, ct);
                    return true;
                }, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(Permissions.DocumentsWrite);

        return app;
    }

    private static DocumentResponse ToDto(DocumentEntity d) =>
        new(d.Id, d.OriginalFilename, d.ContentType, d.Size, d.Status, d.Version, d.CreatedAt);
}

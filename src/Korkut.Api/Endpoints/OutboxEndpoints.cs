using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Korkut.Web.Errors;
using Korkut.Web.Tenancy;
using Korkut.Application.Features.Outbox;
using Korkut.Application.Security;
using Korkut.Domain.Outbox;
using Korkut.Infrastructure.Persistence;

namespace Korkut.Api.Endpoints;

public sealed record OutboxMessageResponse(
    Guid Id, string Kind, string To, string Subject, string Body, DateTimeOffset? SendAt,
    string Status, string? Error, DateTimeOffset CreatedAt, DateTimeOffset? SentAt);

public sealed record OutboxEditRequest
{
    [Required, MaxLength(400)]
    public string? ToAddr { get; init; }

    [MaxLength(500)]
    public string Subject { get; init; } = string.Empty;

    [Required]
    public string? Body { get; init; }

    public DateTimeOffset? SendAt { get; init; }
}

/// <summary>
/// The outbound-message review queue (KDM). Authenticated, tenant-scoped (RLS). A human
/// reviews the assistant's drafts: list/get, edit a draft, confirm (draft → queued) or
/// cancel. Reads use matters.read; edits/confirm/cancel use matters.write.
/// </summary>
public static class OutboxEndpoints
{
    public static IEndpointRouteBuilder MapOutboxEndpoints(this IEndpointRouteBuilder app)
    {
        var outbox = app.MapGroup("/api/outbox");

        outbox.MapGet("", async (ClaimsPrincipal user, KorkutDbContext db, IOutboxService svc, CancellationToken ct) =>
            Results.Ok(await user.InTenantScopeAsync(db, async o => (await svc.ListAsync(o, ct)).Select(ToDto).ToList(), ct)))
            .RequireAuthorization(Permissions.MattersRead);

        outbox.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, IOutboxService svc, CancellationToken ct) =>
            {
                var msg = await user.InTenantScopeAsync(db, o => svc.GetAsync(o, id, ct), ct);
                return msg is null ? throw ApiException.NotFound("Mesaj bulunamadı.") : Results.Ok(ToDto(msg));
            })
            .RequireAuthorization(Permissions.MattersRead);

        outbox.MapPut("/{id:guid}", async (Guid id, OutboxEditRequest body, ClaimsPrincipal user, KorkutDbContext db, IOutboxService svc, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var msg = await user.InTenantScopeAsync(db,
                    o => svc.EditDraftAsync(o, id, body.ToAddr!.Trim(), body.Subject.Trim(), body.Body!, body.SendAt, ct), ct);
                return msg is null ? throw ApiException.NotFound("Taslak mesaj bulunamadı.") : Results.Ok(ToDto(msg));
            })
            .RequireAuthorization(Permissions.MattersWrite);

        outbox.MapPost("/{id:guid}/confirm", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, IOutboxService svc, CancellationToken ct) =>
            {
                var msg = await user.InTenantScopeAsync(db, o => svc.ConfirmAsync(o, id, ct), ct);
                return msg is null ? throw ApiException.NotFound("Onaylanacak taslak bulunamadı.") : Results.Ok(ToDto(msg));
            })
            .RequireAuthorization(Permissions.MattersWrite);

        outbox.MapPost("/{id:guid}/cancel", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, IOutboxService svc, CancellationToken ct) =>
            {
                var msg = await user.InTenantScopeAsync(db, o => svc.CancelAsync(o, id, ct), ct);
                return msg is null ? throw ApiException.NotFound("İptal edilecek mesaj bulunamadı.") : Results.Ok(ToDto(msg));
            })
            .RequireAuthorization(Permissions.MattersWrite);

        return app;
    }

    private static OutboxMessageResponse ToDto(OutboundMessage m) =>
        new(m.Id, m.Kind, m.To, m.Subject, m.Body, m.SendAt, m.Status, m.Error, m.CreatedAt, m.SentAt);
}

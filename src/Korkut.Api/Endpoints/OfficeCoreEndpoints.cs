using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Korkut.Web.Errors;
using Korkut.Web.Tenancy;
using Korkut.Application.Features.OfficeCore;
using Korkut.Application.Security;
using Korkut.Domain.OfficeCore;
using Korkut.Infrastructure.Persistence;

namespace Korkut.Api.Endpoints;

// Transport DTOs (never domain models). Snake_case is applied by the global JSON
// contract, so `ClientId` serializes as `client_id` to match the Python/UI contract.

public sealed record MatterResponse(Guid Id, string Title, Guid? ClientId, string Status, string? CourtNameSnapshot, DateTimeOffset CreatedAt);

/// <summary>Matter detail: the matter plus its hearings (duruşmalar), earliest first.</summary>
public sealed record MatterDetailResponse(
    Guid Id, string Title, Guid? ClientId, string Status, string? CourtNameSnapshot,
    DateTimeOffset CreatedAt, IReadOnlyList<HearingResponse> Hearings);

public sealed record HearingResponse(
    Guid Id, DateTimeOffset ScheduledAt, string? Type, string? Location, string? Result,
    string? Notes, string ReminderStatus);

public sealed record MatterCreateRequest
{
    [Required(ErrorMessage = "Başlık zorunludur.")]
    [MaxLength(500, ErrorMessage = "Başlık en fazla 500 karakter olabilir.")]
    public string? Title { get; init; }

    public Guid? ClientId { get; init; }

    public Guid? CourtId { get; init; }
}

public sealed record HearingCreateRequest
{
    /// <summary>Hearing date (required). Combined with <see cref="Time"/> into the scheduled instant.</summary>
    [Required(ErrorMessage = "Duruşma tarihi zorunludur.")]
    public DateOnly? Date { get; init; }

    /// <summary>Optional time of day; when omitted the hearing is scheduled at midnight (UTC).</summary>
    public TimeOnly? Time { get; init; }

    [MaxLength(100)]
    public string? Type { get; init; }

    [MaxLength(300)]
    public string? Location { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }
}

/// <summary>
/// Office-core CRUD (clients, matters, hearings) on the .NET runtime. Every route:
/// authenticated (deny-by-default permission), tenant-scoped via the token's office claim
/// so RLS applies, and maps DTOs — never domain models. Paths match the frozen UI/Python
/// contract. Data access goes through the Clean-Architecture services (no direct EF here).
/// </summary>
public static class OfficeCoreEndpoints
{
    public static IEndpointRouteBuilder MapOfficeCoreEndpoints(this IEndpointRouteBuilder app)
    {
        MapMatters(app);
        return app;
    }

    private static void MapMatters(IEndpointRouteBuilder app)
    {
        var matters = app.MapGroup("/api/matters");

        matters.MapGet("", async (ClaimsPrincipal user, KorkutDbContext db, IMatterService svc, CancellationToken ct) =>
            Results.Ok(await user.InTenantScopeAsync(db,
                async office => (await svc.ListAsync(office, ct)).Select(ToDto).ToList(), ct)))
            .RequireAuthorization(Permissions.MattersRead);

        matters.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, IMatterService svc, CancellationToken ct) =>
            {
                var detail = await user.InTenantScopeAsync(db, office => svc.GetDetailAsync(office, id, ct), ct);
                return detail is null ? throw ApiException.NotFound("Dosya bulunamadı.") : Results.Ok(ToDto(detail));
            })
            .RequireAuthorization(Permissions.MattersRead);

        matters.MapPost("", async (MatterCreateRequest body, ClaimsPrincipal user, KorkutDbContext db, IMatterService svc, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var created = await user.InTenantScopeAsync(db,
                    office => svc.CreateAsync(office, body.Title!.Trim(), body.ClientId, body.CourtId, ct), ct);
                return Results.Created($"/api/matters/{created.Id}", ToDto(created));
            })
            .RequireAuthorization(Permissions.MattersWrite);

        matters.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, IMatterService svc, CancellationToken ct) =>
            {
                await user.InTenantScopeAsync(db, office => svc.DeleteAsync(office, id, ct), ct);
                return Results.NoContent();
            })
            .RequireAuthorization(Permissions.MattersDelete);

        matters.MapPost("/{id:guid}/hearings", async (Guid id, HearingCreateRequest body, ClaimsPrincipal user, KorkutDbContext db, IMatterService svc, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                // Date is required; time is optional (defaults to midnight). Treated as UTC to
                // match the agenda/calendar comparisons, which are all against UtcNow.
                var scheduledAt = new DateTimeOffset(body.Date!.Value.ToDateTime(body.Time ?? TimeOnly.MinValue), TimeSpan.Zero);
                var hearing = await user.InTenantScopeAsync(db,
                    office => svc.AddHearingAsync(office, id, scheduledAt, body.Type, body.Location, body.Notes, ct), ct);
                return hearing is null
                    ? throw ApiException.NotFound("Dosya bulunamadı.")
                    : Results.Created($"/api/matters/{id}/hearings/{hearing.Id}", ToDto(hearing));
            })
            .RequireAuthorization(Permissions.MattersWrite);
    }

    private static MatterResponse ToDto(Matter m) => new(m.Id, m.Title, m.ClientId, m.Status, m.CourtNameSnapshot, m.CreatedAt);

    private static MatterDetailResponse ToDto(MatterDetail d) => new(
        d.Matter.Id, d.Matter.Title, d.Matter.ClientId, d.Matter.Status, d.Matter.CourtNameSnapshot,
        d.Matter.CreatedAt, d.Hearings.Select(ToDto).ToList());

    private static HearingResponse ToDto(Hearing h) =>
        new(h.Id, h.ScheduledAt, h.Type, h.Location, h.Result, h.Notes, h.ReminderStatus);
}

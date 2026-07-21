using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Korkut.Web.Errors;
using Korkut.Web.Tenancy;
using Korkut.Application.Features.Calendar;
using Korkut.Application.Security;
using Korkut.Domain.Calendar;
using Korkut.Infrastructure.Persistence;

namespace Korkut.Api.Endpoints;

public sealed record CalendarEventResponse(
    Guid Id, string Category, string? Type, string Title, DateTimeOffset StartAt, DateTimeOffset? EndAt, bool AllDay,
    Guid? MatterId, string? MatterTitle, Guid? ClientId, string? ClientName, Guid? AssigneeId, string? AssigneeName,
    string? Location, string Status, string? Result, string? Notes, string? Rrule, int? RemindBeforeMin,
    string ReminderStatus, bool Editable);

public sealed record CalendarEventCreateRequest
{
    [Required, RegularExpression("hearing|deadline|appointment|reminder")]
    public string? Category { get; init; }

    [Required, MaxLength(500)]
    public string? Title { get; init; }

    [Required]
    public DateTimeOffset StartAt { get; init; }

    public DateTimeOffset? EndAt { get; init; }

    public bool AllDay { get; init; }

    [MaxLength(100)]
    public string? Type { get; init; }

    public Guid? MatterId { get; init; }

    public Guid? ClientId { get; init; }

    public Guid? AssigneeId { get; init; }

    [MaxLength(400)]
    public string? Location { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }

    [MaxLength(400)]
    public string? Rrule { get; init; }

    [Range(0, 40320)] // up to 4 weeks
    public int? RemindBeforeMin { get; init; }
}

public sealed record CalendarEventPatchRequest
{
    [MaxLength(500)]
    public string? Title { get; init; }

    [MaxLength(100)]
    public string? Type { get; init; }

    public DateTimeOffset? StartAt { get; init; }

    public DateTimeOffset? EndAt { get; init; }

    public bool? AllDay { get; init; }

    public Guid? MatterId { get; init; }

    public Guid? ClientId { get; init; }

    public Guid? AssigneeId { get; init; }

    [MaxLength(400)]
    public string? Location { get; init; }

    [RegularExpression("scheduled|done|cancelled")]
    public string? Status { get; init; }

    [MaxLength(1000)]
    public string? Result { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }

    [MaxLength(400)]
    public string? Rrule { get; init; }

    [Range(0, 40320)]
    public int? RemindBeforeMin { get; init; }
}

public sealed record AgendaItemResponse(string Kind, DateTimeOffset Date, Guid Id, Guid? MatterId, string Title, string? Status);

public sealed record AgendaResponse(IReadOnlyList<AgendaItemResponse> Items, int Days);

/// <summary>
/// Calendar endpoints on the .NET runtime. Authenticated, tenant-scoped (RLS).
/// <c>GET /events</c> is a UNIFIED read view — editable <c>calendar_events</c> rows
/// plus the office's real hearings and deadlines read-only (so the calendar is
/// complete). Only calendar_events rows can be created/patched/deleted; a hearing or
/// deadline id resolves to 404 here (those are edited through their own domains).
/// <c>GET /upcoming</c> is the agenda (hearings + deadlines in a look-ahead window).
/// Reads use matters.read, writes matters.write.
/// </summary>
public static class CalendarEndpoints
{
    public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        var cal = app.MapGroup("/api/calendar");

        cal.MapGet("/events", async (DateTimeOffset? start, DateTimeOffset? end, string? category, Guid? matterId,
            ClaimsPrincipal user, KorkutDbContext db, ICalendarService svc, CancellationToken ct) =>
            Results.Ok(await user.InTenantScopeAsync(db, async o =>
                (await svc.ListEntriesAsync(o, start, end, category, matterId, ct)).Select(ToDto).ToList(), ct)))
            .RequireAuthorization(Permissions.MattersRead);

        cal.MapPost("/events", async (CalendarEventCreateRequest body, ClaimsPrincipal user, KorkutDbContext db, ICalendarService svc, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var draft = new CalendarEventDraft(
                    body.Category!, body.Title!.Trim(), body.StartAt, body.EndAt, body.AllDay, body.Type?.Trim(),
                    body.MatterId, body.ClientId, body.AssigneeId, body.Location?.Trim(), body.Notes?.Trim(),
                    body.Rrule?.Trim(), body.RemindBeforeMin);
                var created = await user.InTenantScopeAsync(db, o => svc.CreateEventAsync(o, draft, ct), ct);
                return Results.Created($"/api/calendar/events/{created.Id}", ToDto(created));
            })
            .RequireAuthorization(Permissions.MattersWrite);

        cal.MapPatch("/events/{id:guid}", async (Guid id, CalendarEventPatchRequest body, ClaimsPrincipal user, KorkutDbContext db, ICalendarService svc, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var patch = new CalendarEventPatch(
                    body.Title?.Trim(), body.Type?.Trim(), body.StartAt, body.EndAt, body.AllDay, body.MatterId,
                    body.ClientId, body.AssigneeId, body.Location?.Trim(), body.Status, body.Result?.Trim(),
                    body.Notes?.Trim(), body.Rrule?.Trim(), body.RemindBeforeMin);
                var updated = await user.InTenantScopeAsync(db, o => svc.UpdateEventAsync(o, id, patch, ct), ct);
                return updated is null ? throw ApiException.NotFound("Takvim kaydı bulunamadı.") : Results.Ok(ToDto(updated));
            })
            .RequireAuthorization(Permissions.MattersWrite);

        cal.MapDelete("/events/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, ICalendarService svc, CancellationToken ct) =>
            {
                var ok = await user.InTenantScopeAsync(db, o => svc.DeleteEventAsync(o, id, ct), ct);
                return ok ? Results.NoContent() : throw ApiException.NotFound("Takvim kaydı bulunamadı.");
            })
            .RequireAuthorization(Permissions.MattersWrite);

        cal.MapGet("/upcoming", async (int? days, ClaimsPrincipal user, KorkutDbContext db, ICalendarService svc, CancellationToken ct) =>
            {
                var window = Math.Clamp(days ?? 14, 1, 365);
                var items = await user.InTenantScopeAsync(db, async o =>
                    (await svc.AgendaAsync(o, window, ct)).Select(a => new AgendaItemResponse(a.Kind, a.Date, a.Id, a.MatterId, a.Title, a.Status)).ToList(), ct);
                return Results.Ok(new AgendaResponse(items, window));
            })
            .RequireAuthorization(Permissions.MattersRead);

        return app;
    }

    private static CalendarEventResponse ToDto(CalendarEntry r) =>
        new(r.Id, r.Category, r.Type, r.Title, r.StartAt, r.EndAt, r.AllDay, r.MatterId, r.MatterTitle, r.ClientId,
            r.ClientName, r.AssigneeId, r.AssigneeName, r.Location, r.Status, r.Result, r.Notes, r.Rrule,
            r.RemindBeforeMin, r.ReminderStatus, r.Editable);
}

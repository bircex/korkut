using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Korkut.Api.Errors;
using Korkut.Api.Tenancy;
using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;

namespace Korkut.Api.Endpoints;

public sealed record TaskResponse(Guid Id, string Title, string Status, Guid? MatterId, Guid? AssigneeId, DateTimeOffset? DueAt, DateTimeOffset? CompletedAt, DateTimeOffset CreatedAt);

public sealed record TaskCreateRequest
{
    [Required, MaxLength(500)]
    public string? Title { get; init; }

    public Guid? MatterId { get; init; }

    public Guid? AssigneeId { get; init; }

    public DateTimeOffset? DueAt { get; init; }
}

public sealed record TaskStatusRequest
{
    [Required, RegularExpression("open|in_progress|done", ErrorMessage = "status 'open', 'in_progress' veya 'done' olmalıdır.")]
    public string? Status { get; init; }
}

public sealed record DeadlineResponse(Guid Id, string Title, DateOnly DueDate, string Kind, Guid? MatterId, string TimeZoneId, int ReminderDaysBefore, DateTimeOffset? CompletedAt, DateTimeOffset CreatedAt);

public sealed record DeadlineCreateRequest
{
    [Required, MaxLength(500)]
    public string? Title { get; init; }

    [Required]
    public DateOnly DueDate { get; init; }

    [RegularExpression("statutory|internal")]
    public string Kind { get; init; } = "internal";

    public Guid? MatterId { get; init; }

    public string TimeZoneId { get; init; } = "Europe/Istanbul";

    [Range(0, 365)]
    public int ReminderDaysBefore { get; init; }
}

public sealed record NoteResponse(Guid Id, string Body, string Channel, Guid? MatterId, Guid? ClientId, DateTimeOffset OccurredAt, DateTimeOffset CreatedAt);

public sealed record NoteCreateRequest
{
    [Required, MaxLength(4000)]
    public string? Body { get; init; }

    public string Channel { get; init; } = "note";

    public Guid? MatterId { get; init; }

    public Guid? ClientId { get; init; }

    public DateTimeOffset? OccurredAt { get; init; }
}

/// <summary>
/// Office-ops endpoints (tasks, deadlines, communication notes) on the .NET runtime.
/// Authenticated, tenant-scoped (RLS), DTO-mapped. Reads use matters.read, writes
/// matters.write (ops are matter-adjacent work).
/// </summary>
public static class OfficeOpsEndpoints
{
    public static IEndpointRouteBuilder MapOfficeOpsEndpoints(this IEndpointRouteBuilder app)
    {
        MapTasks(app);
        MapDeadlines(app);
        MapNotes(app);
        return app;
    }

    private static void MapTasks(IEndpointRouteBuilder app)
    {
        var tasks = app.MapGroup("/api/tasks");

        tasks.MapGet("", async (Guid? matterId, ClaimsPrincipal user, KorkutDbContext db, OfficeOpsStore store, CancellationToken ct) =>
            Results.Ok(await user.InTenantScopeAsync(db, async o => (await store.ListTasksAsync(o, matterId, ct)).Select(ToDto).ToList(), ct)))
            .RequireAuthorization(Permissions.MattersRead);

        tasks.MapPost("", async (TaskCreateRequest body, ClaimsPrincipal user, KorkutDbContext db, OfficeOpsStore store, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var created = await user.InTenantScopeAsync(db, async o =>
                {
                    var id = await store.CreateTaskAsync(o, body.Title!.Trim(), body.MatterId, body.AssigneeId, body.DueAt, ct);
                    return await store.GetTaskAsync(o, id, ct);
                }, ct);
                return Results.Created($"/api/tasks/{created!.Id}", ToDto(created));
            })
            .RequireAuthorization(Permissions.MattersWrite);

        tasks.MapPatch("/{id:guid}", async (Guid id, TaskStatusRequest body, ClaimsPrincipal user, KorkutDbContext db, OfficeOpsStore store, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var updated = await user.InTenantScopeAsync(db, async o =>
                {
                    var ok = await store.SetTaskStatusAsync(o, id, body.Status!, ct);
                    return ok ? await store.GetTaskAsync(o, id, ct) : null;
                }, ct);
                return updated is null ? throw ApiException.NotFound("Görev bulunamadı.") : Results.Ok(ToDto(updated));
            })
            .RequireAuthorization(Permissions.MattersWrite);

        tasks.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, OfficeOpsStore store, CancellationToken ct) =>
            {
                var ok = await user.InTenantScopeAsync(db, o => store.DeleteTaskAsync(o, id, ct), ct);
                return ok ? Results.NoContent() : throw ApiException.NotFound("Görev bulunamadı.");
            })
            .RequireAuthorization(Permissions.MattersWrite);
    }

    private static void MapDeadlines(IEndpointRouteBuilder app)
    {
        var deadlines = app.MapGroup("/api/deadlines");

        deadlines.MapGet("", async (Guid? matterId, ClaimsPrincipal user, KorkutDbContext db, OfficeOpsStore store, CancellationToken ct) =>
            Results.Ok(await user.InTenantScopeAsync(db, async o => (await store.ListDeadlinesAsync(o, matterId, ct)).Select(ToDto).ToList(), ct)))
            .RequireAuthorization(Permissions.MattersRead);

        deadlines.MapPost("", async (DeadlineCreateRequest body, ClaimsPrincipal user, KorkutDbContext db, OfficeOpsStore store, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var created = await user.InTenantScopeAsync(db, async o =>
                {
                    var id = await store.CreateDeadlineAsync(o, body.Title!.Trim(), body.DueDate, body.Kind, body.MatterId, body.TimeZoneId, body.ReminderDaysBefore, ct);
                    return (await store.ListDeadlinesAsync(o, null, ct)).First(x => x.Id == id);
                }, ct);
                return Results.Created($"/api/deadlines/{created.Id}", ToDto(created));
            })
            .RequireAuthorization(Permissions.MattersWrite);

        deadlines.MapPost("/{id:guid}/complete", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, OfficeOpsStore store, CancellationToken ct) =>
            {
                var ok = await user.InTenantScopeAsync(db, o => store.CompleteDeadlineAsync(o, id, ct), ct);
                return ok ? Results.NoContent() : throw ApiException.NotFound("Süre bulunamadı veya zaten tamamlandı.");
            })
            .RequireAuthorization(Permissions.MattersWrite);

        deadlines.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, OfficeOpsStore store, CancellationToken ct) =>
            {
                var ok = await user.InTenantScopeAsync(db, o => store.DeleteDeadlineAsync(o, id, ct), ct);
                return ok ? Results.NoContent() : throw ApiException.NotFound("Süre bulunamadı.");
            })
            .RequireAuthorization(Permissions.MattersWrite);
    }

    private static void MapNotes(IEndpointRouteBuilder app)
    {
        var notes = app.MapGroup("/api/notes");

        notes.MapGet("", async (Guid? matterId, ClaimsPrincipal user, KorkutDbContext db, OfficeOpsStore store, CancellationToken ct) =>
            Results.Ok(await user.InTenantScopeAsync(db, async o => (await store.ListNotesAsync(o, matterId, ct)).Select(ToDto).ToList(), ct)))
            .RequireAuthorization(Permissions.MattersRead);

        notes.MapPost("", async (NoteCreateRequest body, ClaimsPrincipal user, KorkutDbContext db, OfficeOpsStore store, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var created = await user.InTenantScopeAsync(db, async o =>
                {
                    var id = await store.CreateNoteAsync(o, body.Body!.Trim(), body.Channel, body.MatterId, body.ClientId, body.OccurredAt, ct);
                    return (await store.ListNotesAsync(o, null, ct)).First(x => x.Id == id);
                }, ct);
                return Results.Created($"/api/notes/{created.Id}", ToDto(created));
            })
            .RequireAuthorization(Permissions.MattersWrite);
    }

    private static TaskResponse ToDto(TaskEntity t) => new(t.Id, t.Title, t.Status, t.MatterId, t.AssigneeId, t.DueAt, t.CompletedAt, t.CreatedAt);

    private static DeadlineResponse ToDto(DeadlineEntity d) => new(d.Id, d.Title, d.DueDate, d.Kind, d.MatterId, d.TimeZoneId, d.ReminderDaysBefore, d.CompletedAt, d.CreatedAt);

    private static NoteResponse ToDto(CommunicationNoteEntity n) => new(n.Id, n.Body, n.Channel, n.MatterId, n.ClientId, n.OccurredAt, n.CreatedAt);
}

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Korkut.Agent.Tenancy;
using Korkut.Application.Abstractions.Directory;
using Korkut.Application.Ai;
using Korkut.Application.Features.Agent;
using Korkut.Application.Security;
using Korkut.Domain.Agent;
using Korkut.Infrastructure.Ai;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Agent;
using Korkut.Web.Errors;
using Korkut.Web.Tenancy;

namespace Korkut.Agent.Endpoints;

public sealed record SessionBriefResponse(Guid Id, string Title, Guid? MatterId, string? CreatedByName, DateTimeOffset? UpdatedAt);

public sealed record ChatMessageResponse(string Role, string Content);

public sealed record SessionDetailResponse(
    Guid Id, string Title, Guid? MatterId, string? CreatedByName, DateTimeOffset? UpdatedAt, IReadOnlyList<ChatMessageResponse> Messages);

public sealed record SessionPageResponse(IReadOnlyList<SessionBriefResponse> Items, int Total);

public sealed record RenameSessionRequest
{
    [Required, MaxLength(300)]
    public string? Title { get; init; }
}

public sealed record ChatRequest
{
    [Required, MaxLength(20000)]
    public string? Text { get; init; }
}

public sealed record ChatResponse(string Reply, string Title, Guid? OpenDocument, Guid? PendingEmail);

/// <summary>
/// The assistant's conversations ("sessions") + chat, served by the Agent host. Authenticated,
/// tenant-scoped (RLS). Conversation state lives in the Agent DB; the persona / style / memory
/// a chat turn composes come from the operational DB, so chat/upload/stream pin BOTH contexts
/// (see <see cref="AgentScope"/>). Reads use matters.read; creating, chatting, renaming, cloning
/// and deleting use matters.write. Chat returns 503 when no model provider is configured.
/// </summary>
public static class SessionEndpoints
{
    // SSE frame payloads use the same snake_case contract as the rest of the API.
    private static readonly JsonSerializerOptions SseJson =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var sessions = app.MapGroup("/api/sessions");

        sessions.MapGet("", async (int? limit, int? offset, ClaimsPrincipal user, AgentDbContext db, ISessionService svc, IUserLookup dir, CancellationToken ct) =>
            {
                var take = Math.Clamp(limit ?? 20, 1, 100);
                var skip = Math.Max(offset ?? 0, 0);
                var (items, total) = await user.InTenantScopeAsync(db, o => svc.ListAsync(o, take, skip, ct), ct);
                var names = await ResolveNamesAsync(dir, user.OfficeId(), items, ct);
                return Results.Ok(new SessionPageResponse(items.Select(b => ToBrief(b, names)).ToList(), total));
            })
            .RequireAuthorization(Permissions.MattersRead);

        sessions.MapPost("", async (Guid? matterId, ClaimsPrincipal user, AgentDbContext db, ISessionService svc, IUserLookup dir, CancellationToken ct) =>
            {
                var uid = user.UserId();
                var brief = await user.InTenantScopeAsync(db, async o =>
                {
                    var id = await svc.CreateAsync(o, uid, matterId, ct);
                    return await svc.GetBriefAsync(o, id, ct);
                }, ct);
                return Results.Created($"/api/sessions/{brief!.Id}", await ToBriefAsync(dir, user.OfficeId(), brief, ct));
            })
            .RequireAuthorization(Permissions.MattersWrite);

        sessions.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, AgentDbContext db, ISessionService svc, IUserLookup dir, CancellationToken ct) =>
            {
                var result = await user.InTenantScopeAsync(db, async o =>
                {
                    var brief = await svc.GetBriefAsync(o, id, ct);
                    if (brief is null)
                    {
                        return ((SessionBrief Brief, List<ChatMessageResponse> Messages)?)null;
                    }

                    var messages = (await svc.MessagesAsync(o, id, ct)).Select(m => new ChatMessageResponse(m.Role, m.Content)).ToList();
                    return (brief, messages);
                }, ct);

                if (result is not { } data)
                {
                    throw ApiException.NotFound("Görüşme bulunamadı.");
                }

                var name = await ResolveNameAsync(dir, user.OfficeId(), data.Brief.CreatedBy, ct);
                return Results.Ok(new SessionDetailResponse(
                    data.Brief.Id, data.Brief.Title, data.Brief.MatterId, name, data.Brief.UpdatedAt, data.Messages));
            })
            .RequireAuthorization(Permissions.MattersRead);

        sessions.MapPatch("/{id:guid}", async (Guid id, RenameSessionRequest body, ClaimsPrincipal user, AgentDbContext db, ISessionService svc, IUserLookup dir, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var brief = await user.InTenantScopeAsync(db, async o =>
                    await svc.RenameAsync(o, id, body.Title!.Trim(), ct) ? await svc.GetBriefAsync(o, id, ct) : null, ct);
                return brief is null
                    ? throw ApiException.NotFound("Görüşme bulunamadı.")
                    : Results.Ok(await ToBriefAsync(dir, user.OfficeId(), brief, ct));
            })
            .RequireAuthorization(Permissions.MattersWrite);

        sessions.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, AgentDbContext db, ISessionService svc, CancellationToken ct) =>
            {
                var ok = await user.InTenantScopeAsync(db, o => svc.DeleteAsync(o, id, ct), ct);
                return ok ? Results.Ok(new { ok = true }) : throw ApiException.NotFound("Görüşme bulunamadı.");
            })
            .RequireAuthorization(Permissions.MattersWrite);

        sessions.MapPost("/{id:guid}/clone", async (Guid id, ClaimsPrincipal user, AgentDbContext db, ISessionService svc, IUserLookup dir, CancellationToken ct) =>
            {
                var uid = user.UserId();
                var brief = await user.InTenantScopeAsync(db, async o =>
                {
                    var newId = await svc.CloneAsync(o, id, uid, ct);
                    return newId is null ? null : await svc.GetBriefAsync(o, newId.Value, ct);
                }, ct);
                return brief is null
                    ? throw ApiException.NotFound("Görüşme bulunamadı.")
                    : Results.Ok(await ToBriefAsync(dir, user.OfficeId(), brief, ct));
            })
            .RequireAuthorization(Permissions.MattersWrite);

        sessions.MapPost("/{id:guid}/upload", async (Guid id, HttpContext http, ClaimsPrincipal user, AgentDbContext db, KorkutDbContext office, SessionChatService chat, CancellationToken ct) =>
            {
                if (!http.Request.HasFormContentType)
                {
                    throw ApiException.BadRequest("multipart/form-data bekleniyor.");
                }

                var form = await http.Request.ReadFormAsync(ct);
                var file = form.Files["file"];
                if (file is null || file.Length == 0)
                {
                    throw ApiException.BadRequest("Dosya gerekli.");
                }

                var text = form["text"].ToString();
                var uid = user.UserId();
                var perms = user.Permissions();
                try
                {
                    await using var stream = file.OpenReadStream();
                    var outcome = await user.InAgentAndOfficeScopeAsync(db, office, o =>
                        chat.UploadAsync(o, uid, id, file.FileName, file.ContentType ?? "application/octet-stream", stream, text, perms, ct), ct);
                    return outcome is null
                        ? throw ApiException.NotFound("Görüşme bulunamadı.")
                        : Results.Ok(new { reply = outcome.Reply, title = outcome.Title, user_display = outcome.UserDisplay });
                }
                catch (ModelUnavailableException ex)
                {
                    return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
                }
            })
            .RequireAuthorization(Permissions.MattersWrite)
            .DisableAntiforgery();

        sessions.MapPost("/{id:guid}/chat/stream", async (Guid id, ChatRequest body, HttpContext http, ClaimsPrincipal user, AgentDbContext db, KorkutDbContext office, SessionChatService chat, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                http.Response.Headers.ContentType = "text/event-stream";
                http.Response.Headers.CacheControl = "no-cache";
                var res = http.Response;

                async Task Frame(string ev, object data)
                {
                    await res.WriteAsync($"event: {ev}\ndata: {JsonSerializer.Serialize(data, SseJson)}\n\n", ct);
                    await res.Body.FlushAsync(ct);
                }

                var uid = user.UserId();
                var perms = user.Permissions();
                try
                {
                    var outcome = await user.InAgentAndOfficeScopeAsync(db, office, o => chat.ChatStreamAsync(
                        o, uid, id, body.Text!.Trim(), perms,
                        label => Frame("step", new { label }),
                        token => Frame("token", new { text = token }), ct), ct);

                    if (outcome is null)
                    {
                        await Frame("error", new { detail = "Görüşme bulunamadı." });
                    }
                    else
                    {
                        await Frame("done", new ChatResponse(outcome.Reply, outcome.Title, outcome.OpenDocument, outcome.PendingEmail));
                    }
                }
                catch (ModelUnavailableException ex)
                {
                    await Frame("error", new { detail = ex.Message });
                }
                catch (BudgetExceededException ex)
                {
                    await Frame("error", new { detail = ex.Message });
                }

                return Results.Empty;
            })
            .RequireAuthorization(Permissions.MattersWrite);

        sessions.MapPost("/{id:guid}/chat", async (Guid id, ChatRequest body, ClaimsPrincipal user, AgentDbContext db, KorkutDbContext office, SessionChatService chat, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var uid = user.UserId();
                var perms = user.Permissions();
                try
                {
                    var outcome = await user.InAgentAndOfficeScopeAsync(db, office, o => chat.ChatAsync(o, uid, id, body.Text!.Trim(), perms, ct), ct);
                    return outcome is null
                        ? throw ApiException.NotFound("Görüşme bulunamadı.")
                        : Results.Ok(new ChatResponse(outcome.Reply, outcome.Title, outcome.OpenDocument, outcome.PendingEmail));
                }
                catch (ModelUnavailableException ex)
                {
                    return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
                }
            })
            .RequireAuthorization(Permissions.MattersWrite);

        return app;
    }

    private static async Task<SessionBriefResponse> ToBriefAsync(IUserLookup dir, Guid officeId, SessionBrief b, CancellationToken ct)
        => new(b.Id, b.Title, b.MatterId, await ResolveNameAsync(dir, officeId, b.CreatedBy, ct), b.UpdatedAt);

    private static SessionBriefResponse ToBrief(SessionBrief b, IReadOnlyDictionary<Guid, string> names)
        => new(b.Id, b.Title, b.MatterId, Lookup(names, b.CreatedBy), b.UpdatedAt);

    private static async Task<string?> ResolveNameAsync(IUserLookup dir, Guid officeId, Guid? author, CancellationToken ct)
    {
        if (author is not { } id)
        {
            return null;
        }

        var names = await dir.DisplayNamesAsync(officeId, [id], ct);
        return Lookup(names, author);
    }

    private static async Task<IReadOnlyDictionary<Guid, string>> ResolveNamesAsync(
        IUserLookup dir, Guid officeId, IReadOnlyList<SessionBrief> briefs, CancellationToken ct)
    {
        var ids = briefs.Where(b => b.CreatedBy is not null).Select(b => b.CreatedBy!.Value).Distinct().ToList();
        return await dir.DisplayNamesAsync(officeId, ids, ct);
    }

    private static string? Lookup(IReadOnlyDictionary<Guid, string> names, Guid? author)
        => author is { } id && names.TryGetValue(id, out var name) ? name : null;
}

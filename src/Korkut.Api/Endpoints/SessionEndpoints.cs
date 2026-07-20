using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Korkut.Api.Errors;
using Korkut.Api.Tenancy;
using Korkut.Application.Ai;
using Korkut.Application.Security;
using Korkut.Infrastructure.Ai;
using Korkut.Infrastructure.Persistence;

namespace Korkut.Api.Endpoints;

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
/// The assistant's conversations ("sessions") + chat. Authenticated, tenant-scoped
/// (RLS). Reads use matters.read; creating, chatting, renaming, cloning and deleting use
/// matters.write. Chat returns 503 when no LLM provider is configured.
/// </summary>
public static class SessionEndpoints
{
    // SSE frame payloads use the same snake_case contract as the rest of the API.
    private static readonly JsonSerializerOptions SseJson =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var sessions = app.MapGroup("/api/sessions");

        sessions.MapGet("", async (int? limit, int? offset, ClaimsPrincipal user, KorkutDbContext db, SessionStore store, CancellationToken ct) =>
            {
                var take = Math.Clamp(limit ?? 20, 1, 100);
                var skip = Math.Max(offset ?? 0, 0);
                var (items, total) = await user.InTenantScopeAsync(db, o => store.ListAsync(o, take, skip, ct), ct);
                return Results.Ok(new SessionPageResponse(items.Select(ToBrief).ToList(), total));
            })
            .RequireAuthorization(Permissions.MattersRead);

        sessions.MapPost("", async (Guid? matterId, ClaimsPrincipal user, KorkutDbContext db, SessionStore store, CancellationToken ct) =>
            {
                var uid = user.UserId();
                var brief = await user.InTenantScopeAsync(db, async o =>
                {
                    var id = await store.CreateAsync(o, uid, matterId, ct);
                    return await store.ToBriefAsync(o, (await store.GetAsync(o, id, ct))!, ct);
                }, ct);
                return Results.Created($"/api/sessions/{brief.Id}", ToBrief(brief));
            })
            .RequireAuthorization(Permissions.MattersWrite);

        sessions.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, SessionStore store, CancellationToken ct) =>
            {
                var detail = await user.InTenantScopeAsync(db, async o =>
                {
                    var conv = await store.GetAsync(o, id, ct);
                    if (conv is null)
                    {
                        return null;
                    }

                    var brief = await store.ToBriefAsync(o, conv, ct);
                    var messages = (await store.MessagesAsync(o, id, ct)).Select(m => new ChatMessageResponse(m.Role, m.Content)).ToList();
                    return new SessionDetailResponse(brief.Id, brief.Title, brief.MatterId, brief.CreatedByName, brief.UpdatedAt, messages);
                }, ct);
                return detail is null ? throw ApiException.NotFound("Görüşme bulunamadı.") : Results.Ok(detail);
            })
            .RequireAuthorization(Permissions.MattersRead);

        sessions.MapPatch("/{id:guid}", async (Guid id, RenameSessionRequest body, ClaimsPrincipal user, KorkutDbContext db, SessionStore store, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var brief = await user.InTenantScopeAsync(db, async o =>
                {
                    var ok = await store.RenameAsync(o, id, body.Title!.Trim(), ct);
                    return ok ? await store.ToBriefAsync(o, (await store.GetAsync(o, id, ct))!, ct) : null;
                }, ct);
                return brief is null ? throw ApiException.NotFound("Görüşme bulunamadı.") : Results.Ok(ToBrief(brief));
            })
            .RequireAuthorization(Permissions.MattersWrite);

        sessions.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, SessionStore store, CancellationToken ct) =>
            {
                var ok = await user.InTenantScopeAsync(db, o => store.DeleteAsync(o, id, ct), ct);
                return ok ? Results.Ok(new { ok = true }) : throw ApiException.NotFound("Görüşme bulunamadı.");
            })
            .RequireAuthorization(Permissions.MattersWrite);

        sessions.MapPost("/{id:guid}/clone", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, SessionStore store, CancellationToken ct) =>
            {
                var uid = user.UserId();
                var brief = await user.InTenantScopeAsync(db, async o =>
                {
                    var newId = await store.CloneAsync(o, id, uid, ct);
                    return newId is null ? null : await store.ToBriefAsync(o, (await store.GetAsync(o, newId.Value, ct))!, ct);
                }, ct);
                return brief is null ? throw ApiException.NotFound("Görüşme bulunamadı.") : Results.Ok(ToBrief(brief));
            })
            .RequireAuthorization(Permissions.MattersWrite);

        sessions.MapPost("/{id:guid}/upload", async (Guid id, HttpContext http, ClaimsPrincipal user, KorkutDbContext db, SessionChatService chat, CancellationToken ct) =>
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
                try
                {
                    await using var stream = file.OpenReadStream();
                    var outcome = await user.InTenantScopeAsync(db, o =>
                        chat.UploadAsync(o, uid, id, file.FileName, file.ContentType ?? "application/octet-stream", stream, text, ct), ct);
                    return outcome is null
                        ? throw ApiException.NotFound("Görüşme bulunamadı.")
                        : Results.Ok(new { reply = outcome.Reply, title = outcome.Title, user_display = outcome.UserDisplay });
                }
                catch (LlmUnavailableException ex)
                {
                    return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
                }
            })
            .RequireAuthorization(Permissions.MattersWrite)
            .DisableAntiforgery();

        sessions.MapPost("/{id:guid}/chat/stream", async (Guid id, ChatRequest body, HttpContext http, ClaimsPrincipal user, KorkutDbContext db, SessionChatService chat, CancellationToken ct) =>
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
                try
                {
                    var outcome = await user.InTenantScopeAsync(db, o => chat.ChatStreamAsync(
                        o, uid, id, body.Text!.Trim(),
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
                catch (LlmUnavailableException ex)
                {
                    await Frame("error", new { detail = ex.Message });
                }

                return Results.Empty;
            })
            .RequireAuthorization(Permissions.MattersWrite);

        sessions.MapPost("/{id:guid}/chat", async (Guid id, ChatRequest body, ClaimsPrincipal user, KorkutDbContext db, SessionChatService chat, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var uid = user.UserId();
                try
                {
                    var outcome = await user.InTenantScopeAsync(db, o => chat.ChatAsync(o, uid, id, body.Text!.Trim(), ct), ct);
                    return outcome is null
                        ? throw ApiException.NotFound("Görüşme bulunamadı.")
                        : Results.Ok(new ChatResponse(outcome.Reply, outcome.Title, outcome.OpenDocument, outcome.PendingEmail));
                }
                catch (LlmUnavailableException ex)
                {
                    return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
                }
            })
            .RequireAuthorization(Permissions.MattersWrite);

        return app;
    }

    private static SessionBriefResponse ToBrief(SessionBrief b) => new(b.Id, b.Title, b.MatterId, b.CreatedByName, b.UpdatedAt);
}

using Korkut.Agent.Application.Abstractions;
using Korkut.Agent.Application.Contracts.Sessions;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Agent;
using Korkut.Web.Errors;

namespace Korkut.Agent.Application.Chat;

/// <summary>
/// Chat / upload / streaming as the controller sees them: owns the dual tenant scope (Agent DB +
/// operational DB, both pinned for RLS), resolves the caller from the token, drives
/// <see cref="SessionChatService"/>, and maps to response DTOs. The controller only forwards the
/// request (and, for streaming, the SSE sinks) and returns the result.
/// </summary>
public sealed class ChatAppService(AgentDbContext agent, KorkutDbContext officeDb, ICurrentUser current, SessionChatService chat)
{
    public async Task<ChatResponse> ChatAsync(Guid id, string text, CancellationToken ct = default)
    {
        var outcome = await ScopedAsync(o => chat.ChatAsync(o, current.UserId, id, text.Trim(), current.Permissions, ct), ct);
        return outcome is null ? throw ApiException.NotFound("Görüşme bulunamadı.") : Map(outcome);
    }

    public async Task<UploadResponse> UploadAsync(
        Guid id, string fileName, string contentType, Stream content, string text, CancellationToken ct = default)
    {
        var outcome = await ScopedAsync(
            o => chat.UploadAsync(o, current.UserId, id, fileName, contentType, content, text, current.Permissions, ct), ct);
        return outcome is null
            ? throw ApiException.NotFound("Görüşme bulunamadı.")
            : new UploadResponse(outcome.Reply, outcome.Title, outcome.UserDisplay);
    }

    /// <summary>Streaming turn: the caller supplies the SSE sinks; this owns the scope + orchestration.
    /// Returns null when the conversation is missing (the caller emits an error frame).</summary>
    public Task<ChatResponse?> StreamAsync(
        Guid id, string text, Func<string, Task> onStep, Func<string, Task> onToken, CancellationToken ct = default)
        => ScopedAsync(async o =>
        {
            var outcome = await chat.ChatStreamAsync(o, current.UserId, id, text.Trim(), current.Permissions, onStep, onToken, ct);
            return outcome is null ? null : Map(outcome);
        }, ct);

    private static ChatResponse Map(ChatOutcome o) => new(o.Reply, o.Title, o.OpenDocument, o.PendingEmail);

    private async Task<T> ScopedAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = current.OfficeId;
        await using var agentScope = await agent.BeginTenantScopeAsync(office, ct);
        await using var officeScope = await officeDb.BeginTenantScopeAsync(office, ct);
        var result = await work(office);
        await officeScope.CommitAsync(ct);
        await agentScope.CommitAsync(ct);
        return result;
    }
}

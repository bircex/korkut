using Korkut.Contract.Agent;
using Korkut.Core;
using ICurrentUser = Korkut.Contract.Agent.ICurrentUser;

namespace Korkut.Agent.Chat;

/// <summary>
/// Chat / upload / streaming as the controller sees them: runs <see cref="ISessionChatService"/>
/// inside the dual tenant scope (<see cref="IAgentUnitOfWork"/> — the only DbContext holder),
/// resolves the caller from the token, and maps to response DTOs. The controller only forwards the
/// request (and, for streaming, the SSE sinks) and returns the result.
/// </summary>
public sealed class ChatAppService(IAgentUnitOfWork uow, ICurrentUser current, ISessionChatService chat) : IChatAppService
{
    public async Task<ChatResponse> ChatAsync(Guid id, string text, CancellationToken ct = default)
    {
        var office = current.OfficeId;
        var outcome = await uow.InDualScopeAsync(office,
            () => chat.ChatAsync(office, current.UserId, id, text.Trim(), current.Permissions, ct), ct);
        return outcome is null ? throw new NotFoundException("Görüşme bulunamadı.") : Map(outcome);
    }

    public async Task<UploadResponse> UploadAsync(
        Guid id, string fileName, string contentType, Stream content, string text, CancellationToken ct = default)
    {
        var office = current.OfficeId;
        var outcome = await uow.InDualScopeAsync(office,
            () => chat.UploadAsync(office, current.UserId, id, fileName, contentType, content, text, current.Permissions, ct), ct);
        return outcome is null
            ? throw new NotFoundException("Görüşme bulunamadı.")
            : new UploadResponse(outcome.Reply, outcome.Title, outcome.UserDisplay);
    }

    /// <summary>Streaming turn: the caller supplies the SSE sinks; this owns the scope + orchestration.
    /// Returns null when the conversation is missing (the caller emits an error frame).</summary>
    public Task<ChatResponse?> StreamAsync(
        Guid id, string text, Func<string, Task> onStep, Func<string, Task> onToken, CancellationToken ct = default)
    {
        var office = current.OfficeId;
        return uow.InDualScopeAsync(office, async () =>
        {
            var outcome = await chat.ChatStreamAsync(office, current.UserId, id, text.Trim(), current.Permissions, onStep, onToken, ct);
            return outcome is null ? null : Map(outcome);
        }, ct);
    }

    private static ChatResponse Map(ChatOutcome o) => new(o.Reply, o.Title, o.OpenDocument, o.PendingEmail);
}

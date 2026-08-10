using System.Text.Json;
using Korkut.Agent.Chat.Repositories;
using Korkut.Agent.Chat.Abstractions;
using Korkut.Contract.Agent.Chat;
using Korkut.Contract.Agent;
using Korkut.Core;
using ICurrentUser = Korkut.Contract.Agent.ICurrentUser;

namespace Korkut.Agent.Chat.Services;

/// <summary>
/// Chat / upload / streaming as the controller sees them: resolves the caller from the token, drives
/// <see cref="ISessionChatService"/>, and maps to response DTOs. Tenant-scoping is owned by the chat
/// service itself, which opens SHORT scopes around the DB reads/writes and runs the model loop under
/// no transaction — so a slow turn never pins a connection. The controller only forwards the request
/// (and, for streaming, the SSE sinks) and returns the result.
/// </summary>
internal sealed class ChatAppService(ICurrentUser current, ISessionChatService chat) : IChatAppService
{
    public async Task<ChatResponse> ChatAsync(Guid id, string text, CancellationToken ct = default)
    {
        var outcome = await chat.ChatAsync(current.OfficeId, current.UserId, id, text.Trim(), current.Permissions, ct);
        return outcome is null ? throw new NotFoundException("Görüşme bulunamadı.") : Map(outcome);
    }

    public async Task<UploadResponse> UploadAsync(
        Guid id, string fileName, string contentType, Stream content, string text, CancellationToken ct = default)
    {
        var outcome = await chat.UploadAsync(
            current.OfficeId, current.UserId, id, fileName, contentType, content, text, current.Permissions, ct);
        return outcome is null
            ? throw new NotFoundException("Görüşme bulunamadı.")
            : new UploadResponse(outcome.Reply, outcome.Title, outcome.UserDisplay, outcome.Sources);
    }

    /// <summary>Streaming turn: the caller supplies the SSE sinks. Returns null when the conversation
    /// is missing (the caller emits an error frame).</summary>
    public async Task<ChatResponse?> StreamAsync(
        Guid id, string text, Func<string, Task> onStep, Func<string, Task> onToken,
        Func<string, JsonElement, Task>? onToolCall = null, Func<string, string, bool, Task>? onToolResult = null,
        CancellationToken ct = default)
    {
        var outcome = await chat.ChatStreamAsync(
            current.OfficeId, current.UserId, id, text.Trim(), current.Permissions, onStep, onToken, onToolCall, onToolResult, ct);
        return outcome is null ? null : Map(outcome);
    }

    private static ChatResponse Map(ChatOutcome o)
        => new(o.Reply, o.Title, o.OpenDocument, o.PendingEmail, o.Sources, o.Document);
}

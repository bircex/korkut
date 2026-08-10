using System.Text.Json;

namespace Korkut.Contract.Agent.Chat;

/// <summary>Orchestrates one assistant chat / upload / streaming turn within an already tenant-scoped unit of work.</summary>
public interface ISessionChatService
{
    Task<ChatOutcome?> ChatAsync(
        Guid officeId, Guid userId, Guid conversationId, string text, IReadOnlySet<string> permissions, CancellationToken ct = default);

    Task<UploadOutcome?> UploadAsync(
        Guid officeId, Guid userId, Guid conversationId, string fileName, string contentType, Stream content, string text,
        IReadOnlySet<string> permissions, CancellationToken ct = default);

    /// <summary>
    /// <paramref name="onToolCall"/> fires with a tool's name + raw JSON arguments right before it
    /// runs; <paramref name="onToolResult"/> fires with the same name + result content/error flag
    /// right after — lets the caller (SSE controller) surface a tool-call card, not just an activity label.
    /// </summary>
    Task<ChatOutcome?> ChatStreamAsync(
        Guid officeId, Guid userId, Guid conversationId, string text, IReadOnlySet<string> permissions,
        Func<string, Task> onStep, Func<string, Task> onToken,
        Func<string, JsonElement, Task>? onToolCall = null, Func<string, string, bool, Task>? onToolResult = null,
        CancellationToken ct = default);
}

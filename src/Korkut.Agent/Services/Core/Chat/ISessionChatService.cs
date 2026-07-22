namespace Korkut.Agent.Services.Core.Chat;

/// <summary>Orchestrates one assistant chat / upload / streaming turn within an already tenant-scoped unit of work.</summary>
public interface ISessionChatService
{
    Task<ChatOutcome?> ChatAsync(
        Guid officeId, Guid userId, Guid conversationId, string text, IReadOnlySet<string> permissions, CancellationToken ct = default);

    Task<UploadOutcome?> UploadAsync(
        Guid officeId, Guid userId, Guid conversationId, string fileName, string contentType, Stream content, string text,
        IReadOnlySet<string> permissions, CancellationToken ct = default);

    Task<ChatOutcome?> ChatStreamAsync(
        Guid officeId, Guid userId, Guid conversationId, string text, IReadOnlySet<string> permissions,
        Func<string, Task> onStep, Func<string, Task> onToken, CancellationToken ct = default);
}

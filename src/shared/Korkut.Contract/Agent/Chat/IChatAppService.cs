using System.Text.Json;

namespace Korkut.Contract.Agent.Chat;

/// <summary>Chat / upload / streaming use-cases a controller forwards to.</summary>
public interface IChatAppService
{
    Task<ChatResponse> ChatAsync(Guid id, string text, CancellationToken ct = default);

    Task<UploadResponse> UploadAsync(Guid id, string fileName, string contentType, Stream content, string text, CancellationToken ct = default);

    /// <summary>
    /// <paramref name="onToolCall"/> fires with a tool's name + raw JSON arguments right before it
    /// runs; <paramref name="onToolResult"/> fires with the same name + result content/error flag
    /// right after.
    /// </summary>
    Task<ChatResponse?> StreamAsync(
        Guid id, string text, Func<string, Task> onStep, Func<string, Task> onToken,
        Func<string, JsonElement, Task>? onToolCall = null, Func<string, string, bool, Task>? onToolResult = null,
        CancellationToken ct = default);
}

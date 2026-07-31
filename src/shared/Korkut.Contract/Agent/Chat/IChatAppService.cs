
namespace Korkut.Contract.Agent.Chat;

/// <summary>Chat / upload / streaming use-cases a controller forwards to.</summary>
public interface IChatAppService
{
    Task<ChatResponse> ChatAsync(Guid id, string text, CancellationToken ct = default);

    Task<UploadResponse> UploadAsync(Guid id, string fileName, string contentType, Stream content, string text, CancellationToken ct = default);

    Task<ChatResponse?> StreamAsync(
        Guid id, string text, Func<string, Task> onStep, Func<string, Task> onToken,
        Func<string, Task>? onTool = null, CancellationToken ct = default);
}

namespace Korkut.Contract.Agent.Chat;

public sealed record UploadResponse(string Reply, string Title, string UserDisplay, IReadOnlyList<SourceRef>? Sources = null);

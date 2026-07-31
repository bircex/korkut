namespace Korkut.Contract.Agent.Chat;

/// <summary>The result of a file-upload turn: the assistant reply, the title, and a display line for the user turn.</summary>
public sealed record UploadOutcome(string Reply, string Title, string UserDisplay, IReadOnlyList<SourceRef>? Sources = null);

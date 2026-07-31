namespace Korkut.Contract.Agent.Chat;

public sealed record ChatResponse(string Reply, string Title, Guid? OpenDocument, Guid? PendingEmail, IReadOnlyList<SourceRef>? Sources = null);

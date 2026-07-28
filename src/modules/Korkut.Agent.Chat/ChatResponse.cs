namespace Korkut.Agent.Chat;

public sealed record ChatResponse(string Reply, string Title, Guid? OpenDocument, Guid? PendingEmail);

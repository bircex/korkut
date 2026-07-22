namespace Korkut.Agent.Contracts.Sessions;

public sealed record ChatResponse(string Reply, string Title, Guid? OpenDocument, Guid? PendingEmail);

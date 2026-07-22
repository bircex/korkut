namespace Korkut.Agent.Services.Core.Chat;

/// <summary>The result of a chat turn: the assistant reply, the (possibly auto-set) title, and UI hints.</summary>
public sealed record ChatOutcome(string Reply, string Title, Guid? OpenDocument, Guid? PendingEmail);

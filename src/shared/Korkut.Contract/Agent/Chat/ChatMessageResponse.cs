namespace Korkut.Contract.Agent.Chat;

public sealed record ChatMessageResponse(string Role, string Content, IReadOnlyList<SourceRef>? Sources = null);

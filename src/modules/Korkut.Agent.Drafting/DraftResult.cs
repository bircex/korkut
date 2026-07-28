namespace Korkut.Agent.Drafting;

/// <summary>A staged draft edit produced by the assistant: the pending version id + its content/summary.</summary>
public sealed record DraftResult(Guid PendingId, string ContentMd, string Summary);

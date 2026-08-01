namespace Korkut.Contract.Agent.Chat;

/// <summary>The result of a chat turn: the assistant reply, the (possibly auto-set) title, and UI hints.
/// <see cref="Document"/> is the draft the turn produced (when any); <see cref="OpenDocument"/> mirrors
/// its id for the original open_document contract field.</summary>
public sealed record ChatOutcome(
    string Reply, string Title, Guid? OpenDocument, Guid? PendingEmail,
    IReadOnlyList<SourceRef>? Sources = null, DocumentRef? Document = null);

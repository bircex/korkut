namespace Korkut.Contract.Agent.Chat;

/// <summary>
/// The draft document a chat turn produced/updated — the UI's "belge kartı" + editor-open signal.
/// Emitted as its own SSE frame (`document`) and carried on the terminal payload.
/// </summary>
public sealed record DocumentRef(Guid DocId, string Title, string? Summary);

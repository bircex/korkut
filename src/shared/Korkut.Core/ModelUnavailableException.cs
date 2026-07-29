namespace Korkut.Core;

/// <summary>
/// Raised when no provider is configured or a model call fails; surfaced as 503. Stays in the shared
/// Application layer because the Knowledge host's embedder path throws it and its endpoints catch it
/// (the chat side, moved to the Agent host, throws it too).
/// </summary>
public sealed class ModelUnavailableException(string message) : Exception(message);

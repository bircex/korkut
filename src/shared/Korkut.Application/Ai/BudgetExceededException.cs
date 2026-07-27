namespace Korkut.Application.Ai;

/// <summary>
/// Raised when an office has exhausted its model-usage budget; surfaced as 429. Stays in the shared
/// Application layer because the Web error handler (used by every host) maps it; the governor that
/// throws it moved to the Agent host.
/// </summary>
public sealed class BudgetExceededException(string message) : Exception(message);

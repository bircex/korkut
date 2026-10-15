namespace Korkut.Agent.Chat.Abstractions;

/// <summary>
/// Which conversations a listing returns: the caller's own personal ones, plus those of the matters
/// in their scope (all matters, or <see cref="MatterIds"/>), optionally narrowed to one matter or to
/// personal conversations only.
/// </summary>
public sealed record SessionFilter(
    Guid UserId, bool AllMatters, IReadOnlyCollection<Guid> MatterIds, Guid? MatterId = null, bool PersonalOnly = false);

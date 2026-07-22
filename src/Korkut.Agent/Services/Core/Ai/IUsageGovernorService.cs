namespace Korkut.Agent.Services.Core.Ai;

/// <summary>
/// Cost/rate governance for model calls (principle 9). Before a call, the caller enters the
/// governor: it enforces the per-office <b>token budget</b> (derived from the <c>llm_calls</c>
/// ledger) and the per-office <b>concurrency cap</b>. The returned handle MUST be disposed when the
/// call finishes to release the concurrency slot. Both limits are opt-in (unset → unlimited).
/// </summary>
public interface IUsageGovernorService
{
    Task<IAsyncDisposable> BeginCallAsync(Guid officeId, CancellationToken ct = default);
}

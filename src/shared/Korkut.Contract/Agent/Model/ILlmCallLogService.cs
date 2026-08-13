
namespace Korkut.Contract.Agent.Model;

/// <summary>
/// Records one completed model call for cost aggregation, per-office billing, and eval (principle
/// 8/9). Every call carries the <see cref="ChatUsage"/> seam (provider / model / tokens) already;
/// this persists it — scoped to the caller's office AND attributed to the user who triggered it —
/// alongside the derived cost + latency + the feature that made the call (chat / drafting / …).
/// Best-effort: a failure here must never break the turn it is recording.
/// </summary>
public interface ILlmCallLogService
{
    /// <param name="userId">The member the call is billed to; null when no human initiated it (jobs).</param>
    Task RecordAsync(
        Guid officeId, Guid? userId, string feature, ChatUsage usage, TimeSpan latency, string promptFingerprint,
        CancellationToken ct = default);
}

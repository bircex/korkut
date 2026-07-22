using Korkut.Agent.Application.Ai;

namespace Korkut.Agent.Application.Abstractions.Ai;

/// <summary>
/// Records one completed model call for cost aggregation, per-office billing, and eval (principle
/// 8/9). Every call carries the <see cref="ChatUsage"/> seam (provider / model / tokens) already;
/// this persists it — scoped to the caller's office — alongside the derived cost + latency + the
/// feature that made the call (chat / drafting / …). Best-effort: a failure here must never break
/// the turn it is recording.
/// </summary>
public interface ILlmCallLog
{
    Task RecordAsync(
        Guid officeId, string feature, ChatUsage usage, TimeSpan latency, string promptFingerprint, CancellationToken ct = default);
}

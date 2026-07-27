using Korkut.Agent.Model;
using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;

namespace Korkut.Application.Agent.Abstractions;

/// <summary>
/// Records one completed model call for cost aggregation, per-office billing, and eval (principle
/// 8/9). Every call carries the <see cref="ChatUsage"/> seam (provider / model / tokens) already;
/// this persists it — scoped to the caller's office — alongside the derived cost + latency + the
/// feature that made the call (chat / drafting / …). Best-effort: a failure here must never break
/// the turn it is recording.
/// </summary>
public interface ILlmCallLogService
{
    Task RecordAsync(
        Guid officeId, string feature, ChatUsage usage, TimeSpan latency, string promptFingerprint, CancellationToken ct = default);
}

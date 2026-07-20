namespace Korkut.Application.Differential;

/// <summary>Mismatch telemetry for a differential/shadow run (KDM-034).</summary>
public sealed record MismatchTelemetry(int Total, int High, int Low, IReadOnlyList<string> HighPaths);

/// <summary>
/// Turns differential results into a go/no-go decision for the dual-read / traffic-
/// switch cutover (KDM-034). The rollback criterion is explicit and conservative:
/// <b>any unresolved high-severity mismatch on a critical flow blocks the cutover and
/// triggers rollback.</b> Low-severity (cosmetic) mismatches are reported but allowed.
/// </summary>
public static class RolloutPolicy
{
    public static MismatchTelemetry Summarize(IEnumerable<Mismatch> mismatches)
    {
        var list = mismatches.ToList();
        var high = list.Where(m => m.Severity == MismatchSeverity.High).ToList();
        return new MismatchTelemetry(list.Count, high.Count, list.Count - high.Count, high.Select(m => m.Path).ToList());
    }

    /// <summary>True only when it is safe to shift traffic to .NET (no high-severity mismatch).</summary>
    public static bool CanCutOver(MismatchTelemetry telemetry) => telemetry.High == 0;

    /// <summary>The rollback trigger: the inverse of <see cref="CanCutOver"/>.</summary>
    public static bool MustRollback(MismatchTelemetry telemetry) => telemetry.High > 0;
}

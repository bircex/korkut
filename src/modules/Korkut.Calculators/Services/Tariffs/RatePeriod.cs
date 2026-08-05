namespace Korkut.Calculators.Services.Tariffs;

/// <summary>
/// One validity window of a time-varying rate (annual %). <paramref name="To"/> is exclusive;
/// null = still in force. Tariff tables are ordered, contiguous arrays of these — updating a
/// tariff means closing the last period and appending a row, never touching tool code.
/// </summary>
internal sealed record RatePeriod(DateOnly From, DateOnly? To, decimal AnnualPercent);

/// <summary>Slices a date range across a <see cref="RatePeriod"/> table (for piecewise simple interest).</summary>
internal static class RateSchedule
{
    public sealed record Segment(DateOnly From, DateOnly To, decimal AnnualPercent, int Days);

    /// <summary>True when <paramref name="start"/> falls inside the table's coverage.</summary>
    public static bool Covers(IReadOnlyList<RatePeriod> table, DateOnly start) => start >= table[0].From;

    /// <summary>Overlaps of [start, endExclusive) with each period, oldest first.</summary>
    public static IReadOnlyList<Segment> Segments(IReadOnlyList<RatePeriod> table, DateOnly start, DateOnly endExclusive)
    {
        var result = new List<Segment>();
        foreach (var period in table)
        {
            var from = period.From > start ? period.From : start;
            var to = period.To is { } t && t < endExclusive ? t : endExclusive;
            if (from >= to)
            {
                continue;
            }

            result.Add(new Segment(from, to, period.AnnualPercent, to.DayNumber - from.DayNumber));
        }

        return result;
    }
}

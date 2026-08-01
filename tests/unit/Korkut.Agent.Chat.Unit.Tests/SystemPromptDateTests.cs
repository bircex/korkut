using Korkut.Agent.Chat.Services;

namespace Korkut.Agent.Chat.Unit.Tests;

/// <summary>
/// The "Bugünün tarihi" line injected into every system prompt: it must carry the wall-clock
/// date/time in Turkey (fixed UTC+3, no DST) with hand-rolled Turkish month/day names, because
/// the runtime is InvariantGlobalization and the containers ship without tzdata.
/// </summary>
public class SystemPromptDateTests
{
    [Fact]
    public void Formats_turkish_date_day_name_and_time_at_utc_plus_three()
    {
        // 2026-08-02 11:45 UTC → 14:45 in Turkey; 2 Ağustos 2026 is a Sunday (Pazar).
        var line = SessionChatService.TurkeyTodayLine(new DateTimeOffset(2026, 8, 2, 11, 45, 0, TimeSpan.Zero));

        Assert.Contains("2 Ağustos 2026 Pazar", line);
        Assert.Contains("saat 14:45", line);
        Assert.Contains("Türkiye saati, UTC+3", line);
    }

    [Fact]
    public void Converts_across_midnight_so_the_turkish_calendar_day_is_used()
    {
        // 22:30 UTC on Dec 31 is already 01:30 on Jan 1 in Turkey — the new day (and year) must win.
        var line = SessionChatService.TurkeyTodayLine(new DateTimeOffset(2026, 12, 31, 22, 30, 0, TimeSpan.Zero));

        Assert.Contains("1 Ocak 2027", line);
        Assert.Contains("saat 01:30", line);
    }

    [Fact]
    public void Non_utc_input_offsets_are_normalized_to_turkey_time()
    {
        // The same instant given with a -05:00 offset must produce the same Turkish wall clock.
        var line = SessionChatService.TurkeyTodayLine(new DateTimeOffset(2026, 8, 2, 6, 45, 0, TimeSpan.FromHours(-5)));

        Assert.Contains("2 Ağustos 2026 Pazar", line);
        Assert.Contains("saat 14:45", line);
    }
}

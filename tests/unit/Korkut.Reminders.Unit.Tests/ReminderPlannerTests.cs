using Korkut.Contract.Deadlines;
using Korkut.Contract.Hearings;
using Korkut.Contract.Tasks;
using Korkut.Reminders.Services;

namespace Korkut.Reminders.Unit.Tests;

/// <summary>When each kind of reminder comes due, in office time (Türkiye, UTC+3).</summary>
public class ReminderPlannerTests
{
    private static readonly Dictionary<Guid, string> Matters = [];
    private static readonly Guid MatterId = Guid.NewGuid();

    static ReminderPlannerTests() => Matters[MatterId] = "2026/123 Esas";

    private static DateTimeOffset Utc(int month, int day, int hour, int minute = 0) => new(2026, month, day, hour, minute, 0, TimeSpan.Zero);

    private static DeadlineResponse Deadline(DateOnly due, int daysBefore, DateTimeOffset? completedAt = null, string zone = "Europe/Istanbul")
        => new(Guid.NewGuid(), "Cevap dilekçesi", due, "internal", completedAt is null ? "open" : "done", MatterId, null, zone, daysBefore, completedAt, Utc(10, 1, 0));

    private static TaskCalendarRow Task(
        DateTimeOffset start, int? remindBeforeMin, string category = "gorev", Guid? assignee = null, bool allDay = false, string status = "open")
        => new(Guid.NewGuid(), category, null, "Müvekkili ara", start, null, allDay, MatterId, null, assignee, null, status, null, null, remindBeforeMin, "pending");

    [Fact]
    public void A_deadline_reminder_comes_at_nine_in_the_morning_office_time()
    {
        // Due Thursday 15 Oct, two days ahead: Tuesday 13 Oct 09:00 in Türkiye is 06:00 UTC.
        var deadline = Deadline(new DateOnly(2026, 10, 15), 2);

        Assert.Empty(ReminderPlanner.Deadlines([deadline], Matters, Utc(10, 13, 5, 59)));
        var due = Assert.Single(ReminderPlanner.Deadlines([deadline], Matters, Utc(10, 13, 6)));

        Assert.Equal(Utc(10, 13, 6), due.At);
        Assert.Null(due.Recipients);
        Assert.Equal(
            "Süre: Cevap dilekçesi\nSon gün: 15 Ekim 2026 Perşembe (2 gün kaldı)\nDosya: 2026/123 Esas",
            ReminderPlanner.Text(due));
    }

    [Fact]
    public void A_late_deadline_reminder_still_goes_until_the_last_day_ends()
    {
        var deadline = Deadline(new DateOnly(2026, 10, 15), 2);

        var due = Assert.Single(ReminderPlanner.Deadlines([deadline], Matters, Utc(10, 15, 20, 59)));
        Assert.Contains("(bugün)", due.Lines[0]);
        Assert.Empty(ReminderPlanner.Deadlines([deadline], Matters, Utc(10, 15, 21))); // 16 Oct 00:00 local
    }

    [Fact]
    public void A_completed_deadline_is_not_reminded()
        => Assert.Empty(ReminderPlanner.Deadlines([Deadline(new DateOnly(2026, 10, 15), 2, Utc(10, 12, 0))], Matters, Utc(10, 14, 6)));

    [Fact]
    public void An_unknown_zone_falls_back_to_office_time()
        => Assert.Equal(Utc(10, 15, 6), Assert.Single(ReminderPlanner.Deadlines([Deadline(new DateOnly(2026, 10, 15), 0, zone: "Nowhere/Here")], Matters, Utc(10, 15, 7))).At);

    [Fact]
    public void A_hearing_is_reminded_the_morning_before_until_it_starts()
    {
        var hearing = new HearingCalendarRow(Guid.NewGuid(), MatterId, Utc(10, 14, 11, 30), "Ön inceleme", "İstanbul 5. Asliye Hukuk");

        Assert.Empty(ReminderPlanner.Hearings([hearing], Matters, Utc(10, 13, 5)));
        var due = Assert.Single(ReminderPlanner.Hearings([hearing], Matters, Utc(10, 13, 6)));
        Assert.Equal(
            "Duruşma: 2026/123 Esas\nTarih: 14 Ekim 2026 Çarşamba, 14:30\nYer: İstanbul 5. Asliye Hukuk\nTür: Ön inceleme",
            ReminderPlanner.Text(due));
        Assert.Empty(ReminderPlanner.Hearings([hearing], Matters, Utc(10, 14, 11, 30)));
    }

    [Fact]
    public void An_agenda_record_goes_to_its_assignee_ahead_by_its_own_lead()
    {
        var assignee = Guid.NewGuid();
        var task = Task(Utc(10, 14, 11), 30, assignee: assignee);

        Assert.Empty(ReminderPlanner.Agenda([task], Matters, Utc(10, 14, 10, 29)));
        var due = Assert.Single(ReminderPlanner.Agenda([task], Matters, Utc(10, 14, 10, 30)));
        Assert.Equal([assignee], due.Recipients);
        Assert.StartsWith("Görev: Müvekkili ara\nZaman: 14 Ekim 2026 Çarşamba, 14:00", ReminderPlanner.Text(due));
    }

    [Fact]
    public void Unassigned_records_go_to_everyone()
        => Assert.Null(Assert.Single(ReminderPlanner.Agenda([Task(Utc(10, 14, 11), 0)], Matters, Utc(10, 14, 11))).Recipients);

    [Fact]
    public void Records_without_a_lead_are_reminded_only_when_they_are_reminders()
    {
        Assert.Empty(ReminderPlanner.Agenda([Task(Utc(10, 14, 11), null)], Matters, Utc(10, 14, 11)));
        var due = Assert.Single(ReminderPlanner.Agenda([Task(Utc(10, 14, 11), null, "hatirlatma")], Matters, Utc(10, 14, 11)));
        Assert.StartsWith("Hatırlatma:", due.Title);
    }

    [Fact]
    public void Done_and_long_past_records_are_not_reminded()
    {
        Assert.Empty(ReminderPlanner.Agenda([Task(Utc(10, 14, 11), 30, status: "done")], Matters, Utc(10, 14, 10, 45)));
        Assert.Empty(ReminderPlanner.Agenda([Task(Utc(10, 14, 11), 30)], Matters, Utc(10, 14, 12)));
    }

    [Fact]
    public void An_all_day_record_starts_at_nine()
    {
        var task = Task(Utc(10, 14, 0), 60, "randevu", allDay: true);

        var due = Assert.Single(ReminderPlanner.Agenda([task], Matters, Utc(10, 14, 5)));
        Assert.Equal(Utc(10, 14, 5), due.At); // 09:00 local less an hour
        Assert.Contains("Tarih: 14 Ekim 2026 Çarşamba", due.Lines[0]);
    }

    [Fact]
    public void Moving_a_date_sets_up_a_new_reminder()
    {
        var deadline = Deadline(new DateOnly(2026, 10, 15), 0);
        var moved = deadline with { DueDate = new DateOnly(2026, 10, 16), ReminderDaysBefore = 1 };

        var before = Assert.Single(ReminderPlanner.Deadlines([deadline], Matters, Utc(10, 15, 7)));
        var after = Assert.Single(ReminderPlanner.Deadlines([moved], Matters, Utc(10, 15, 7)));

        Assert.Equal(before.At, after.At);
        Assert.NotEqual(before.Key, after.Key);
    }
}

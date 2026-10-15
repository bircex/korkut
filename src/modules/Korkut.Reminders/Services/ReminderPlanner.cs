using Korkut.Contract.Deadlines;
using Korkut.Contract.Hearings;
using Korkut.Contract.Tasks;

namespace Korkut.Reminders.Services;

/// <summary>
/// A reminder whose moment has come. It goes to <see cref="Recipients"/> when the planner names them
/// (a task's assignee, or whoever added a record with no matter), otherwise to the team of
/// <see cref="MatterId"/>: never to someone who cannot see the matter.
/// </summary>
internal sealed record DueReminder(
    string Key, string Kind, Guid ItemId, DateTimeOffset At, IReadOnlyCollection<Guid>? Recipients, string Title,
    IReadOnlyList<string> Lines, Guid? MatterId = null);

/// <summary>
/// Decides which reminders are due, from the office's records and the current moment. Pure: no
/// clock, no database, so the calendar rules can be checked on their own.
/// <list type="bullet">
/// <item>Deadline: at 09:00 office time, <c>ReminderDaysBefore</c> days before the last day, until that day ends (to the matter's team).</item>
/// <item>Hearing: at 09:00 the day before, until it starts (to the matter's team).</item>
/// <item>Agenda record: <c>RemindBeforeMin</c> before it starts (a hatırlatma with none, when it starts),
/// to its assignee or, unassigned, to whoever added it; an all-day record "starts" at 09:00. Late by up to an hour is still useful.</item>
/// </list>
/// A reminder that came due while Korkut was down still goes out, as long as the item is ahead.
/// </summary>
internal static class ReminderPlanner
{
    public const string OfficeTimeZone = "Europe/Istanbul";
    public static readonly TimeOnly Morning = new(9, 0);
    private static readonly TimeSpan TaskGrace = TimeSpan.FromHours(1);

    // The app runs with invariant globalization and without tzdata in its hardened image, so
    // Turkish names are spelled out here and Turkey (UTC+3 all year since 2016) has a fixed zone.
    private static readonly string[] Months =
        ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];

    private static readonly string[] Days = ["Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi"];

    private static readonly TimeZoneInfo Turkey =
        TimeZoneInfo.CreateCustomTimeZone(OfficeTimeZone, TimeSpan.FromHours(3), "Türkiye", "Türkiye");

    public static IEnumerable<DueReminder> Deadlines(
        IEnumerable<DeadlineResponse> deadlines, IReadOnlyDictionary<Guid, string> matters, DateTimeOffset now)
    {
        foreach (var d in deadlines)
        {
            if (d.CompletedAt is not null)
            {
                continue;
            }

            var zone = Zone(d.TimeZoneId);
            var at = AtLocal(d.DueDate.AddDays(-d.ReminderDaysBefore), Morning, zone);
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
            if (now < at || today > d.DueDate)
            {
                continue;
            }

            var left = d.DueDate.DayNumber - today.DayNumber;
            var lines = new List<string>
            {
                $"Son gün: {Date(d.DueDate)} ({(left == 0 ? "bugün" : left == 1 ? "yarın" : $"{left} gün kaldı")})",
            };
            AddMatter(lines, d.MatterId, matters);
            IReadOnlyCollection<Guid>? owner = d.MatterId is null && d.CreatedBy is { } by ? [by] : null;
            yield return new DueReminder(
                Key("deadline", d.Id, d.DueDate.DayNumber, at), "deadline", d.Id, at, owner, $"Süre: {d.Title}", lines, d.MatterId);
        }
    }

    public static IEnumerable<DueReminder> Hearings(
        IEnumerable<HearingCalendarRow> hearings, IReadOnlyDictionary<Guid, string> matters, DateTimeOffset now)
    {
        var zone = Zone(OfficeTimeZone);
        foreach (var h in hearings)
        {
            var local = TimeZoneInfo.ConvertTime(h.ScheduledAt, zone);
            var at = AtLocal(DateOnly.FromDateTime(local.DateTime).AddDays(-1), Morning, zone);
            if (now < at || now >= h.ScheduledAt)
            {
                continue;
            }

            var lines = new List<string> { $"Tarih: {When(local)}" };
            if (!string.IsNullOrWhiteSpace(h.Location))
            {
                lines.Add($"Yer: {h.Location}");
            }

            if (!string.IsNullOrWhiteSpace(h.Type))
            {
                lines.Add($"Tür: {h.Type}");
            }

            var title = matters.TryGetValue(h.MatterId, out var matter) ? $"Duruşma: {matter}" : "Duruşma";
            yield return new DueReminder(
                Key("hearing", h.Id, h.ScheduledAt.ToUnixTimeSeconds(), at), "hearing", h.Id, at, null, title, lines, h.MatterId);
        }
    }

    public static IEnumerable<DueReminder> Agenda(
        IEnumerable<TaskCalendarRow> records, IReadOnlyDictionary<Guid, string> matters, DateTimeOffset now)
    {
        var zone = Zone(OfficeTimeZone);
        foreach (var t in records)
        {
            var lead = t.RemindBeforeMin ?? (t.Category == "hatirlatma" ? 0 : (int?)null);
            if (lead is null || t.Status == "done")
            {
                continue;
            }

            var local = TimeZoneInfo.ConvertTime(t.StartAt, zone);
            var starts = t.AllDay ? AtLocal(DateOnly.FromDateTime(local.DateTime), Morning, zone) : t.StartAt;
            var at = starts.AddMinutes(-lead.Value);
            if (now < at || now >= starts + TaskGrace)
            {
                continue;
            }

            var lines = new List<string>
            {
                t.AllDay
                    ? $"Tarih: {Date(DateOnly.FromDateTime(local.DateTime))}"
                    : $"Zaman: {When(local)}",
            };
            if (!string.IsNullOrWhiteSpace(t.Location))
            {
                lines.Add($"Yer: {t.Location}");
            }

            AddMatter(lines, t.MatterId, matters);
            var label = t.Category switch { "randevu" => "Randevu", "hatirlatma" => "Hatırlatma", _ => "Görev" };
            // The assignee; unassigned, whoever added it; with neither, the matter's team.
            IReadOnlyCollection<Guid>? to = t.AssigneeId is { } assignee ? [assignee] : t.CreatedBy is { } by ? [by] : null;
            yield return new DueReminder(
                Key("task", t.Id, t.StartAt.ToUnixTimeSeconds(), at), "task", t.Id, at, to, $"{label}: {t.Title}", lines, t.MatterId);
        }
    }

    /// <summary>The reminder as one Telegram message: the title, then its details.</summary>
    public static string Text(DueReminder reminder) => string.Join('\n', [reminder.Title, .. reminder.Lines]);

    private static void AddMatter(List<string> lines, Guid? matterId, IReadOnlyDictionary<Guid, string> matters)
    {
        if (matterId is { } id && matters.TryGetValue(id, out var title))
        {
            lines.Add($"Dosya: {title}");
        }
    }

    /// <summary>Names the item, when it is and when it is reminded: changing either sets up a new reminder.</summary>
    private static string Key(string kind, Guid id, long when, DateTimeOffset at) => $"{kind}:{id:N}:{when}:{at.ToUnixTimeSeconds()}";

    /// <summary>"12 Ekim 2026 Pazartesi".</summary>
    internal static string Date(DateOnly date) => $"{date.Day} {Months[date.Month - 1]} {date.Year} {Days[(int)date.DayOfWeek]}";

    /// <summary>"12 Ekim 2026 Pazartesi, 14:30".</summary>
    internal static string When(DateTimeOffset local) => $"{Date(DateOnly.FromDateTime(local.DateTime))}, {local:HH\\:mm}";

    /// <summary>The record's zone; Turkey's fixed zone when that is the one asked for or the system has no such zone.</summary>
    internal static TimeZoneInfo Zone(string id)
        => id != OfficeTimeZone && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) ? zone : Turkey;

    private static DateTimeOffset AtLocal(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}

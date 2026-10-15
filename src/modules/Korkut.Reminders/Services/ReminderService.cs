using Korkut.Contract.Deadlines;
using Korkut.Contract.Hearings;
using Korkut.Contract.Matters;
using Korkut.Contract.Notifications;
using Korkut.Contract.Settings;
using Korkut.Contract.Tasks;
using Korkut.Contract.Telegram;
using Korkut.Core;
using Korkut.Reminders.Entities;
using Korkut.Reminders.Repositories;
using Microsoft.Extensions.Logging;

namespace Korkut.Reminders.Services;

/// <summary>
/// One pass: find the reminders that are due, mark them sent, then deliver them on Telegram to the
/// people they concern (a matter's team, a task's assignee, whoever added a record with no matter)
/// and to the office's notification address. Marking comes first, so a crash mid-way
/// loses a reminder rather than sending it twice.
/// </summary>
public sealed partial class ReminderService(
    IDeadlineLookup deadlines, IHearingLookup hearings, ITaskLookup agenda, IMatterLookup matters,
    INotificationAddressLookup addresses, ISentReminderRepository sent, IUnitOfWork unitOfWork,
    ITelegramNotifier telegram, IEmailSender email, MailLinks links, ILogger<ReminderService> logger)
{
    // How far ahead reminders can reach: deadlines up to 365 days, agenda records up to four weeks.
    private static readonly TimeSpan DeadlineHorizon = TimeSpan.FromDays(367);
    private static readonly TimeSpan HearingHorizon = TimeSpan.FromDays(3);
    private static readonly TimeSpan AgendaHorizon = TimeSpan.FromDays(29);

    /// <summary>Sends what is due at <paramref name="now"/>; returns how many reminders went out.</summary>
    public async Task<int> RunOnceAsync(Guid officeId, DateTimeOffset now, CancellationToken ct = default)
    {
        var (due, address, teams) = await unitOfWork.InTenantScopeAsync(officeId, async () =>
        {
            var d = await deadlines.ListInRangeAsync(officeId, now.AddDays(-2), now + DeadlineHorizon, null, ct);
            var h = await hearings.ListInRangeAsync(officeId, now, now + HearingHorizon, null, ct);
            var t = await agenda.ListInRangeAsync(officeId, now.AddDays(-2), now + AgendaHorizon, null, ct);
            var matterIds = d.Select(x => x.MatterId).Concat(t.Select(x => x.MatterId)).OfType<Guid>()
                .Concat(h.Select(x => x.MatterId)).Distinct().ToList();
            var titles = await matters.TitlesAsync(officeId, matterIds, ct);

            var candidates = ReminderPlanner.Deadlines(d, titles, now)
                .Concat(ReminderPlanner.Hearings(h, titles, now))
                .Concat(ReminderPlanner.Agenda(t, titles, now))
                .ToList();
            var already = await sent.SentAsync(officeId, candidates.Select(c => c.Key).ToList(), ct);
            var fresh = candidates.Where(c => !already.Contains(c.Key)).ToList();
            foreach (var reminder in fresh)
            {
                sent.Add(new SentReminder(officeId, reminder.Key, reminder.Kind, reminder.ItemId, now));
            }

            await sent.SaveChangesAsync(ct);
            return fresh.Count > 0
                ? (fresh, await addresses.AddressAsync(officeId, ct), await matters.TeamsAsync(officeId, ct))
                : (fresh, null, new Dictionary<Guid, IReadOnlyList<Guid>>());
        }, ct);

        foreach (var reminder in due)
        {
            // Named people, or the matter's team; nobody who cannot see the matter.
            IReadOnlyCollection<Guid> to = reminder.Recipients
                ?? (reminder.MatterId is { } m && teams.TryGetValue(m, out var team) ? team : []);
            await DeliverAsync(officeId, reminder, to, address, ct);
        }

        return due.Count;
    }

    private async Task DeliverAsync(Guid officeId, DueReminder reminder, IReadOnlyCollection<Guid> to, string? address, CancellationToken ct)
    {
        try
        {
            if (to.Count > 0)
            {
                await telegram.NotifyAsync(officeId, to, ReminderPlanner.Text(reminder), ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            NotDelivered(logger, "Telegram", ex);
        }

        if (address is null)
        {
            return;
        }

        try
        {
            var html = EmailTemplates.Render(reminder.Title, reminder.Lines, ("Korkut'u aç", links.LoginLink()));
            await email.SendAsync(address, $"Hatırlatma: {reminder.Title}", html, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            NotDelivered(logger, "e-mail", ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A reminder could not be delivered by {Channel}.")]
    private static partial void NotDelivered(ILogger logger, string channel, Exception ex);
}

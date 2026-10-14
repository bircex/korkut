using Korkut.Contract.Notifications;
using Korkut.Contract.Telegram;
using Korkut.Core;
using Korkut.Deadlines.Entities;
using Korkut.Hearings.Entities;
using Korkut.Matters.Entities;
using Korkut.Organization.Entities;
using Korkut.Reminders.Services;
using Korkut.Settings.Entities;
using Korkut.Tasks.Entities;
using Korkut.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Korkut.Reminders.Integration.Tests;

/// <summary>
/// A reminder pass over the real database with RLS: what is due goes out once, to the right
/// members on Telegram and to the office's notification address. Telegram and SMTP are stand-ins.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class ReminderServiceTests(KorkutApiFactory factory)
{
    private sealed class Notices : ITelegramNotifier
    {
        public List<(IReadOnlyCollection<Guid>? To, string Text)> Sent { get; } = [];

        public Task<int> NotifyAsync(Guid officeId, IReadOnlyCollection<Guid>? userIds, string text, CancellationToken ct = default)
        {
            Sent.Add((userIds, text));
            return Task.FromResult(1);
        }
    }

    private sealed class Mail : IEmailSender
    {
        public List<(string To, string Subject)> Sent { get; } = [];

        public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        {
            Sent.Add((to, subject));
            return Task.CompletedTask;
        }
    }

    private (WebApplicationFactory<Program> App, Notices Telegram, Mail Mail) Host()
    {
        var (telegram, mail) = (new Notices(), new Mail());
        var app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<ITelegramNotifier>();
            s.AddSingleton<ITelegramNotifier>(telegram);
            s.RemoveAll<IEmailSender>();
            s.AddSingleton<IEmailSender>(mail);
        }));
        return (app, telegram, mail);
    }

    private static async Task<int> RunAsync(WebApplicationFactory<Program> app, Guid office, DateTimeOffset now)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ReminderService>().RunOnceAsync(office, now);
    }

    /// <summary>An office with an owner and a lawyer, a matter, and one due deadline, hearing and task.</summary>
    private async Task<(Guid Office, string OwnerEmail, Guid Lawyer)> SeedAsync(DateTimeOffset now, bool mailOff = false)
    {
        var (office, ownerId) = await factory.SeedOfficeAsync();
        await using var db = factory.OwnerContext();
        var ownerEmail = db.Users.Single(u => u.Id == ownerId).Email;
        var lawyer = new User(office, $"avukat-{Guid.CreateVersion7():N}@example.com", Roles.Lawyer, null, null, now);
        db.Users.Add(lawyer);
        var matter = new Matter(office, "2026/77 Esas", null, null, null, null, null, null, null, now);
        db.Matters.Add(matter);
        var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(3)).DateTime);
        db.Deadlines.Add(new Deadline(office, "İstinaf süresi", today.AddDays(2), "internal", matter.Id, "Europe/Istanbul", 5, null, now));
        db.MatterHearings.Add(new Hearing(office, matter.Id, now.AddHours(3), "Tahkikat", "Ankara 3. İş Mahkemesi", null));
        db.Tasks.Add(new WorkTask(office, "gorev", "Bilirkişi raporuna itiraz", null, null, matter.Id, lawyer.Id, null, now.AddMinutes(20), null, false, null, null, 30, now));
        // Not due: a task with no reminder asked for.
        db.Tasks.Add(new WorkTask(office, "gorev", "Arşiv düzeni", null, null, null, null, null, now.AddMinutes(20), null, false, null, null, null, now));
        if (mailOff)
        {
            var preference = new MailPreference(office);
            preference.Update(false, null, now);
            db.MailPreferences.Add(preference);
        }

        await db.SaveChangesAsync();
        return (office, ownerEmail, lawyer.Id);
    }

    [Fact]
    public async Task What_is_due_goes_out_once_to_the_right_people()
    {
        var (app, telegram, mail) = Host();
        var now = DateTimeOffset.UtcNow;
        var (office, ownerEmail, lawyer) = await SeedAsync(now);

        Assert.Equal(3, await RunAsync(app, office, now));

        Assert.Equal(3, telegram.Sent.Count);
        Assert.Contains(telegram.Sent, n => n.To is null && n.Text.StartsWith("Süre: İstinaf süresi\n", StringComparison.Ordinal) && n.Text.Contains("Dosya: 2026/77 Esas"));
        Assert.Contains(telegram.Sent, n => n.To is null && n.Text.StartsWith("Duruşma: 2026/77 Esas\n", StringComparison.Ordinal));
        Assert.Contains(telegram.Sent, n => n.To is { } to && to.SequenceEqual([lawyer]) && n.Text.StartsWith("Görev: Bilirkişi raporuna itiraz", StringComparison.Ordinal));
        Assert.All(mail.Sent, m => Assert.Equal(ownerEmail, m.To));
        Assert.Contains(mail.Sent, m => m.Subject == "Hatırlatma: Süre: İstinaf süresi");
        Assert.Equal(3, mail.Sent.Count);

        // A minute later nothing is sent again.
        Assert.Equal(0, await RunAsync(app, office, now.AddMinutes(1)));
        Assert.Equal(3, telegram.Sent.Count);
    }

    [Fact]
    public async Task With_notification_mail_off_only_Telegram_is_used()
    {
        var (app, telegram, mail) = Host();
        var now = DateTimeOffset.UtcNow;
        var (office, _, _) = await SeedAsync(now, mailOff: true);

        Assert.Equal(3, await RunAsync(app, office, now));

        Assert.Equal(3, telegram.Sent.Count);
        Assert.Empty(mail.Sent);
    }

    [Fact]
    public async Task One_office_is_never_reminded_of_anothers_records()
    {
        var (app, telegram, _) = Host();
        var now = DateTimeOffset.UtcNow;
        await SeedAsync(now);
        var (quiet, _) = await factory.SeedOfficeAsync();

        Assert.Equal(0, await RunAsync(app, quiet, now));
        Assert.Empty(telegram.Sent);
    }
}

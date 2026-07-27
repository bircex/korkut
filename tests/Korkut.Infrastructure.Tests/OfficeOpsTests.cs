using Korkut.Organization;
using Korkut.Organization.Entities;
using Korkut.Infrastructure.Persistence;
using Korkut.Core;
using Microsoft.EntityFrameworkCore;
using Korkut.Deadlines;
using Korkut.Deadlines.Entities;
using Korkut.Tasks;
using Korkut.Tasks.Entities;
using Korkut.Infrastructure.Deadlines;

namespace Korkut.Infrastructure.Tests;

public class OfficeOpsTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private Guid _office;

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch.AddYears(56);
    }

    public async Task InitializeAsync()
    {
        await pg.ResetAsync();
        _office = Guid.CreateVersion7();
        await using var db = pg.NewContext();
        db.Offices.Add(new Office(_office, "O", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --- timezone / business-date semantics (unit) ---

    [Fact]
    public void Reminder_instant_is_local_midnight_in_the_deadline_zone_not_utc()
    {
        // Due 2026-01-01 in Europe/Istanbul (UTC+3), remind on the day → local midnight
        // is 2025-12-31T21:00:00Z, NOT 2026-01-01T00:00:00Z.
        var deadline = new Deadline(Guid.Empty, "d", new DateOnly(2026, 1, 1), "internal", null, "Europe/Istanbul", 0, DateTimeOffset.UtcNow);
        var instant = DeadlineReminderService.ReminderInstant(deadline);
        Assert.Equal(new DateTimeOffset(2025, 12, 31, 21, 0, 0, TimeSpan.Zero), instant);
    }

    [Fact]
    public void Reminder_days_before_shifts_the_business_date()
    {
        var deadline = new Deadline(Guid.Empty, "d", new DateOnly(2026, 3, 10), "internal", null, "UTC", 3, DateTimeOffset.UtcNow);
        var instant = DeadlineReminderService.ReminderInstant(deadline);
        Assert.Equal(new DateTimeOffset(2026, 3, 7, 0, 0, 0, TimeSpan.Zero), instant);
    }

    // --- reminder idempotency (real Postgres) ---

    [Fact]
    public async Task Ensuring_a_reminder_twice_enqueues_it_only_once()
    {
        var deadline = new Deadline(
            _office, "Cevap süresi", new DateOnly(2026, 5, 1), "internal", null, "Europe/Istanbul", 2, DateTimeOffset.UtcNow);
        await using (var db = pg.NewContext())
        {
            db.Deadlines.Add(deadline);
            await db.SaveChangesAsync();
        }

        await using (var db = pg.NewContext())
        {
            await new DeadlineReminderService(db, new Clock()).EnsureReminderAsync(_office, deadline);
        }

        await using (var db = pg.NewContext())
        {
            await new DeadlineReminderService(db, new Clock()).EnsureReminderAsync(_office, deadline); // again
        }

        await using var check = pg.NewContext();
        var reminders = await check.Outbox.AsNoTracking()
            .Where(x => x.IdempotencyKey == DeadlineReminderService.ReminderKey(deadline.Id)).ToListAsync();
        Assert.Single(reminders);
        // And it was scheduled for the correct tz-aware instant.
        Assert.Equal(DeadlineReminderService.ReminderInstant(deadline), reminders[0].AvailableAt);
    }

    // --- task lifecycle + tenant isolation (real Postgres) ---

    [Fact]
    public async Task Task_status_lifecycle_persists()
    {
        Guid id;
        await using (var db = pg.NewContext())
        {
            var task = new WorkTask(_office, "Dilekçe yaz", null, null, null, DateTimeOffset.UtcNow);
            db.Tasks.Add(task);
            await db.SaveChangesAsync();
            id = task.Id;
        }

        await using (var db = pg.NewContext())
        {
            await db.Tasks.Where(x => x.Id == id).ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Status, "done").SetProperty(x => x.CompletedAt, DateTimeOffset.UtcNow), default);
        }

        await using var check = pg.NewContext();
        var t = await check.Tasks.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal("done", t.Status);
        Assert.NotNull(t.CompletedAt);
    }
}

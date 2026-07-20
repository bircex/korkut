using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

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
        db.Offices.Add(new OfficeEntity { Id = _office, Name = "O", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --- timezone / business-date semantics (unit) ---

    [Fact]
    public void Reminder_instant_is_local_midnight_in_the_deadline_zone_not_utc()
    {
        // Due 2026-01-01 in Europe/Istanbul (UTC+3), remind on the day → local midnight
        // is 2025-12-31T21:00:00Z, NOT 2026-01-01T00:00:00Z.
        var deadline = new DeadlineEntity { DueDate = new DateOnly(2026, 1, 1), TimeZoneId = "Europe/Istanbul", ReminderDaysBefore = 0 };
        var instant = DeadlineService.ReminderInstant(deadline);
        Assert.Equal(new DateTimeOffset(2025, 12, 31, 21, 0, 0, TimeSpan.Zero), instant);
    }

    [Fact]
    public void Reminder_days_before_shifts_the_business_date()
    {
        var deadline = new DeadlineEntity { DueDate = new DateOnly(2026, 3, 10), TimeZoneId = "UTC", ReminderDaysBefore = 3 };
        var instant = DeadlineService.ReminderInstant(deadline);
        Assert.Equal(new DateTimeOffset(2026, 3, 7, 0, 0, 0, TimeSpan.Zero), instant);
    }

    // --- reminder idempotency (real Postgres) ---

    [Fact]
    public async Task Ensuring_a_reminder_twice_enqueues_it_only_once()
    {
        var deadline = new DeadlineEntity
        {
            Id = Guid.CreateVersion7(), OfficeId = _office, Title = "Cevap süresi",
            DueDate = new DateOnly(2026, 5, 1), TimeZoneId = "Europe/Istanbul", ReminderDaysBefore = 2,
        };
        await using (var db = pg.NewContext())
        {
            db.Deadlines.Add(deadline);
            await db.SaveChangesAsync();
        }

        await using (var db = pg.NewContext())
        {
            await new DeadlineService(db, new Clock()).EnsureReminderAsync(_office, deadline);
        }

        await using (var db = pg.NewContext())
        {
            await new DeadlineService(db, new Clock()).EnsureReminderAsync(_office, deadline); // again
        }

        await using var check = pg.NewContext();
        var reminders = await check.Outbox.AsNoTracking()
            .Where(x => x.IdempotencyKey == DeadlineService.ReminderKey(deadline.Id)).ToListAsync();
        Assert.Single(reminders);
        // And it was scheduled for the correct tz-aware instant.
        Assert.Equal(DeadlineService.ReminderInstant(deadline), reminders[0].AvailableAt);
    }

    // --- task lifecycle + tenant isolation (real Postgres) ---

    [Fact]
    public async Task Task_status_lifecycle_persists()
    {
        var id = Guid.CreateVersion7();
        await using (var db = pg.NewContext())
        {
            db.Tasks.Add(new TaskEntity { Id = id, OfficeId = _office, Title = "Dilekçe yaz", Status = "open", CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
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

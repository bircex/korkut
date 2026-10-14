using Korkut.Reminders.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Reminders.Repositories;

/// <summary>EF/Postgres adapter for <see cref="ISentReminderRepository"/> over the ambient DbContext.</summary>
internal sealed class SentReminderRepository(DbContext db) : ISentReminderRepository
{
    public async Task<IReadOnlySet<string>> SentAsync(Guid officeId, IReadOnlyCollection<string> keys, CancellationToken ct = default)
    {
        if (keys.Count == 0)
        {
            return new HashSet<string>();
        }

        var list = keys.ToList();
        return (await db.Set<SentReminder>().AsNoTracking()
            .Where(r => r.OfficeId == officeId && list.Contains(r.Key))
            .Select(r => r.Key)
            .ToListAsync(ct)).ToHashSet();
    }

    public void Add(SentReminder reminder) => db.Set<SentReminder>().Add(reminder);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

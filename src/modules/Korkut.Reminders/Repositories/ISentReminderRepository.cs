using Korkut.Reminders.Entities;

namespace Korkut.Reminders.Repositories;

/// <summary>Persistence port for sent reminders; runs in the caller's tenant scope.</summary>
public interface ISentReminderRepository
{
    /// <summary>Which of these keys were already sent.</summary>
    Task<IReadOnlySet<string>> SentAsync(Guid officeId, IReadOnlyCollection<string> keys, CancellationToken ct = default);

    void Add(SentReminder reminder);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

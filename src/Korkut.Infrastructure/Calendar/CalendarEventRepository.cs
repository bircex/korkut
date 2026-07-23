using Korkut.Application.Calendar;
using Korkut.Domain.Calendar;
using Korkut.Infrastructure.Common;
using Korkut.Infrastructure.Persistence;

namespace Korkut.Infrastructure.Calendar;

/// <summary>EF/Postgres adapter for <see cref="ICalendarEventRepository"/>.</summary>
public sealed class CalendarEventRepository(KorkutDbContext db)
    : Repository<CalendarEvent>(db), ICalendarEventRepository;

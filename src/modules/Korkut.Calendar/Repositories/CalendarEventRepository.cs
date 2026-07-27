using Korkut.Calendar.Entities;
using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Calendar.Repositories;

/// <summary>EF/Postgres adapter for <see cref="ICalendarEventRepository"/> over the ambient DbContext.</summary>
internal sealed class CalendarEventRepository(DbContext db)
    : Repository<CalendarEvent>(db), ICalendarEventRepository;

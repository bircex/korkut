using Korkut.Calendar.Entities;
using Korkut.Core;

namespace Korkut.Calendar.Repositories;

/// <summary>Persistence port for the editable calendar-event aggregate.</summary>
internal interface ICalendarEventRepository : IRepository<CalendarEvent>;

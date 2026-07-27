using Korkut.Core;

namespace Korkut.Calendar;

/// <summary>Persistence port for the editable calendar-event aggregate.</summary>
public interface ICalendarEventRepository : IRepository<CalendarEvent>;

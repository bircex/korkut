using Korkut.Core;
using Korkut.Domain.Calendar;

namespace Korkut.Application.Calendar;

/// <summary>Persistence port for the editable calendar-event aggregate.</summary>
public interface ICalendarEventRepository : IRepository<CalendarEvent>;

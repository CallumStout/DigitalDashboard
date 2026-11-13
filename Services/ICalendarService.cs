using DigitalDashboard.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalDashboard.Services;

public interface ICalendarService
{
    Task<IReadOnlyList<CalendarEventModel>> GetUpcomingEventsAsync(CancellationToken cancellationToken = default);
}

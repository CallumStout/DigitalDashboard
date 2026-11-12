using System;

namespace DigitalDashboard.Services;

public class ClockService : IClockService
{
    public DateTime GetCurrentTime()
    {
        return DateTime.Now;
    }
}

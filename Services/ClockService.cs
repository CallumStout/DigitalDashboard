using System;

namespace DigitalClock.Services;

public class ClockService : IClockService
{
    public DateTime GetCurrentTime()
    {
        return DateTime.Now;
    }
}

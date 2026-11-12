using System;

namespace DigitalClock.Services;

public interface IClockService
{
    DateTime GetCurrentTime();
}

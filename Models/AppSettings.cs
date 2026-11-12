using System.Collections.Generic;

namespace DigitalClock.Models;

public class AppSettings
{
    public required List<WeatherLocation> WeatherLocations { get; init; }
}

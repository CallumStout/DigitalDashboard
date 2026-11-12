using System.Collections.Generic;

namespace DigitalDashboard.Models;

public class AppSettings
{
    public required List<WeatherLocation> WeatherLocations { get; init; }
}

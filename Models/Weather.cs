using System.Collections.Generic;
using Material.Icons;

namespace DigitalDashboard.Models;

public class Weather
{
    public double Temperature { get; init; }
    public string Description { get; init; } = string.Empty;
    public MaterialIconKind Icon { get; init; } = MaterialIconKind.Minus;
    public double? High { get; init; }
    public double? Humidity { get; init; }
    public double? WindSpeed { get; init; }
    public double? WindDirection { get; init; }
    public double? UvIndex { get; init; }
    public IReadOnlyList<WeatherForecast> Forecast { get; init; } = [];
}

public record WeatherForecast(string Time, string Temperature, MaterialIconKind Icon, string Description);

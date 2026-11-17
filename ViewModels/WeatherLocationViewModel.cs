using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using DigitalDashboard.Models;

namespace DigitalDashboard.ViewModels;

public partial class WeatherLocationViewModel : ViewModelBase
{
    public WeatherLocation Location { get; }
    public string Name => Location.Name;
    [ObservableProperty] private string _temperature = "--";
    [ObservableProperty] private string _description = "Loading weather…";
    [ObservableProperty] private string _icon = "–";
    [ObservableProperty] private string _humidity = "–";
    [ObservableProperty] private string _wind = "–";
    [ObservableProperty] private string _uv = "–";
    [ObservableProperty] private IReadOnlyList<WeatherForecast> _forecast = [];

    public WeatherLocationViewModel(WeatherLocation location) => Location = location;

    public void Update(Weather weather)
    {
        Temperature = $"{weather.Temperature:0}";
        Description = weather.Description + (weather.High is double high ? $" · High {high:0}°" : "");
        Icon = weather.Icon;
        Humidity = weather.Humidity is double humidity ? $"{humidity:0}%" : "–";
        string[] directions = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
        var direction = weather.WindDirection is double degrees
            ? " " + directions[(int)Math.Round(degrees / 45) % 8] : "";
        Wind = weather.WindSpeed is double speed ? $"{speed:0} mph{direction}" : "–";
        Uv = weather.UvIndex is double uv ? $"{uv:0.#}" : "–";
        Forecast = weather.Forecast;
    }

    public void SetUnavailable()
    {
        Temperature = "--";
        Description = "Weather unavailable";
        Icon = Humidity = Wind = Uv = "–";
        Forecast = [];
    }
}

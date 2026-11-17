using DigitalDashboard.Models;
using System;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalDashboard.Services;

public class WeatherService : IWeatherService
{
    private readonly HttpClient _httpClient;

    public WeatherService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Weather> GetCurrentWeatherAsync(WeatherLocation location, CancellationToken cancellationToken = default)
    {
        var weatherUrl = string.Create(CultureInfo.InvariantCulture,
            $"https://api.open-meteo.com/v1/forecast?latitude={location.Latitude}&longitude={location.Longitude}")
            + "&current=temperature_2m,weather_code,relative_humidity_2m,wind_speed_10m,wind_direction_10m,is_day&temperature_unit=celsius&wind_speed_unit=mph&hourly=temperature_2m,weather_code,is_day,uv_index&daily=temperature_2m_max&timezone=auto&forecast_days=2";

        var response = await _httpClient.GetFromJsonAsync<WeatherResponse>(weatherUrl, cancellationToken);

        if (response?.Current == null)
        {
            throw new InvalidDataException("Open-Meteo did not return current weather.");
        }

        var hourly = response.Hourly;
        var nextHours = hourly?.Time.Select((time, index) => (time, index))
            .Where(item => item.time > response.Current.Time).Take(12).ToArray() ?? [];
        var currentHour = hourly?.Time.Select((time, index) => (time, index))
            .LastOrDefault(item => item.time <= response.Current.Time).index ?? 0;

        return new Weather
        {
            Temperature = response.Current.Temperature,
            Description = GetDescription(response.Current.WeatherCode),
            Icon = GetIcon(response.Current.WeatherCode, response.Current.IsDay),
            Humidity = response.Current.Humidity,
            WindSpeed = response.Current.WindSpeed,
            WindDirection = response.Current.WindDirection,
            High = response.Daily?.High.FirstOrDefault(),
            UvIndex = hourly?.Uv.ElementAtOrDefault(currentHour),
            Forecast = nextHours.Where((_, index) => index % 3 == 0).Select(item => new WeatherForecast(
                item.time.ToString("HH:mm", CultureInfo.InvariantCulture),
                hourly!.Temperature.ElementAtOrDefault(item.index) is double temperature ? $"{temperature:0}°" : "–",
                GetIcon(hourly!.Code.ElementAtOrDefault(item.index), hourly.IsDay.ElementAtOrDefault(item.index)),
                GetDescription(hourly.Code.ElementAtOrDefault(item.index)))).ToArray()
        };
    }

    private static string GetIcon(int? code, int? isDay) => code switch
    {
        0 or 1 => isDay == 0 ? "☾" : "☀",
        2 => isDay == 0 ? "☾" : "⛅",
        3 => "☁",
        45 or 48 => "≋",
        >= 51 and <= 67 or >= 80 and <= 82 => "☂",
        >= 71 and <= 77 or 85 or 86 => "❄",
        >= 95 => "ϟ",
        _ => "–"
    };

    private static string GetDescription(int? weatherCode)
    {
        return weatherCode switch
        {
            0 => "Clear sky",
            1 => "Mainly clear",
            2 => "Partly cloudy",
            3 => "Overcast",
            45 or 48 => "Foggy",
            51 or 53 or 55 => "Drizzle",
            56 or 57 => "Freezing drizzle",
            61 or 63 or 65 => "Rain",
            66 or 67 => "Freezing rain",
            71 or 73 or 75 or 77 => "Snow",
            80 or 81 or 82 => "Rain showers",
            85 or 86 => "Snow showers",
            95 or 97 => "Thunderstorm",
            96 or 99 => "Thunderstorm with hail",
            _ => "Unknown conditions"
        };
    }

    private class WeatherResponse
    {
        [JsonPropertyName("current")]
        public required CurrentWeather? Current { get; init; }
        [JsonPropertyName("hourly")] public HourlyWeather? Hourly { get; init; }
        [JsonPropertyName("daily")] public DailyWeather? Daily { get; init; }
    }

    private class HourlyWeather
    {
        [JsonPropertyName("time")] public DateTime[] Time { get; init; } = [];
        [JsonPropertyName("temperature_2m")] public double?[] Temperature { get; init; } = [];
        [JsonPropertyName("weather_code")] public int?[] Code { get; init; } = [];
        [JsonPropertyName("is_day")] public int?[] IsDay { get; init; } = [];
        [JsonPropertyName("uv_index")] public double?[] Uv { get; init; } = [];
    }

    private class DailyWeather
    {
        [JsonPropertyName("temperature_2m_max")] public double?[] High { get; init; } = [];
    }

    private class CurrentWeather
    {
        [JsonPropertyName("time")] public DateTime Time { get; init; }
        [JsonPropertyName("relative_humidity_2m")] public double? Humidity { get; init; }
        [JsonPropertyName("wind_speed_10m")] public double? WindSpeed { get; init; }
        [JsonPropertyName("wind_direction_10m")] public double? WindDirection { get; init; }
        [JsonPropertyName("is_day")] public int? IsDay { get; init; }
        [JsonPropertyName("temperature_2m")]
        public required double Temperature { get; init; }

        [JsonPropertyName("weather_code")]
        public required int WeatherCode { get; init; }
    }
}

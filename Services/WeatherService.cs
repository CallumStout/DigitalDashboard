using DigitalDashboard.Models;
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
            + "&current=temperature_2m,weather_code&temperature_unit=celsius";

        var response = await _httpClient.GetFromJsonAsync<WeatherResponse>(weatherUrl, cancellationToken);

        if (response?.Current == null)
        {
            throw new InvalidDataException("Open-Meteo did not return current weather.");
        }

        return new Weather
        {
            Temperature = response.Current.Temperature,
            Description = GetDescription(response.Current.WeatherCode)
        };
    }

    private static string GetDescription(int weatherCode)
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
    }

    private class CurrentWeather
    {
        [JsonPropertyName("temperature_2m")]
        public required double Temperature { get; init; }

        [JsonPropertyName("weather_code")]
        public required int WeatherCode { get; init; }
    }
}

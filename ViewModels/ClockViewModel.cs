
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DigitalDashboard.Services;
using DigitalDashboard.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalDashboard.ViewModels;

public partial class ClockViewModel : ViewModelBase, IDisposable
{
    private readonly IClockService _clockService;
    private readonly IWeatherService _weatherService;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _weatherTimer;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _isUpdatingWeather;
    private bool _isDisposed;

    [ObservableProperty]
    private string _time = string.Empty;

    [ObservableProperty]
    private string _date = string.Empty;

    public IReadOnlyList<WeatherLocationViewModel> WeatherLocations { get; }

    public bool HasNoWeatherLocations => WeatherLocations.Count == 0;

    public ClockViewModel(IClockService clockService, IWeatherService weatherService, AppSettings settings)
    {
        _clockService = clockService;
        _weatherService = weatherService;
        WeatherLocations = settings.WeatherLocations
            .Select(location => new WeatherLocationViewModel(location))
            .ToArray();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        _timer.Tick += (_, _) => UpdateClock();

        UpdateClock();
        _timer.Start();

        _weatherTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(10)
        };

        _weatherTimer.Tick += async (_, _) => await UpdateWeatherAsync();

        _ = UpdateWeatherAsync();
        _weatherTimer.Start();
    }

    private void UpdateClock()
    {
        var now = _clockService.GetCurrentTime();

        Time = now.ToString("HH:mm:ss");
        Date = now.ToString("dddd, dd MMMM yyyy");
    }

    private async Task UpdateWeatherAsync()
    {
        if (_isUpdatingWeather || _isDisposed)
        {
            return;
        }

        _isUpdatingWeather = true;

        try
        {
            await Task.WhenAll(WeatherLocations.Select(UpdateLocationWeatherAsync));
        }
        finally
        {
            _isUpdatingWeather = false;
        }
    }

    private async Task UpdateLocationWeatherAsync(WeatherLocationViewModel location)
    {
        try
        {
            var weather = await _weatherService.GetCurrentWeatherAsync(location.Location, _cancellationTokenSource.Token);

            if (!_isDisposed)
            {
                location.Temperature = $"{weather.Temperature:0}";
                location.Description = weather.Description;
            }
        }
        catch (OperationCanceledException) when (_isDisposed)
        {
        }
        catch (Exception exception) when (exception is HttpRequestException
            or OperationCanceledException or JsonException or InvalidDataException)
        {
            Debug.WriteLine($"Unable to update weather for {location.Name}: {exception.Message}");

            if (!_isDisposed)
            {
                location.Temperature = "--";
                location.Description = "Weather unavailable";
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _timer.Stop();
        _weatherTimer.Stop();
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}

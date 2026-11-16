using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalDashboard.Models;
using DigitalDashboard.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    private readonly ICalendarService _calendarService;
    private readonly ISmartDeviceService _smartDeviceService;
    private readonly HashSet<SmartDeviceViewModel> _trackedDevices = [];
    private readonly DispatcherTimer _calendarTimer;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _weatherTimer;
    private readonly DispatcherTimer _devicesTimer;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _isUpdatingWeather;
    private bool _isUpdatingCalendar;
    private bool _isUpdatingDevices;
    private bool _isDisposed;

    [ObservableProperty]
    private IReadOnlyList<CalendarEventModel> _calendarEvents = [];

    [ObservableProperty]
    private string _time = string.Empty;

    [ObservableProperty]
    private string _date = string.Empty;

    public IReadOnlyList<WeatherLocationViewModel> WeatherLocations { get; }

    public ObservableCollection<SmartDeviceViewModel> SmartDevices { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOverviewLight))]
    private SmartDeviceViewModel? _overviewLight;

    public bool HasOverviewLight => OverviewLight != null;
    public bool HasNoSmartPlugs => SmartDevices.Count == 0;
    public string PlugSummary => SmartDevices.Count == 0 ? "No plugs connected" :
        $"{SmartDevices.Count(p => p.HasState && p.IsOn)} on · {SmartDevices.Count(p => p.HasState && !p.IsOn)} off";
    public string PlugAvailability => _deviceLoadStatus ?? (SmartDevices.Count == 0 ? "Your plugs will appear here" :
        $"{SmartDevices.Count} configured · {SmartDevices.Count(p => !p.HasState)} unknown");
    private string? _deviceLoadStatus;

    public SpotifyViewModel Spotify { get; }

    public bool HasNoWeatherLocations => WeatherLocations.Count == 0;

    public bool HasNoCalendarEvents => CalendarEvents.Count == 0;

    public ClockViewModel(IClockService clockService, IWeatherService weatherService, AppSettings settings, ICalendarService calendarService, ISmartDeviceService smartDeviceService, SpotifyViewModel spotifyViewModel)
    {
        _clockService = clockService;
        _weatherService = weatherService;
        _calendarService = calendarService;
        _smartDeviceService = smartDeviceService;

        Spotify = spotifyViewModel;

        WeatherLocations = settings.WeatherLocations
            .Select(location => new WeatherLocationViewModel(location))
            .ToArray();

        SmartDevices.CollectionChanged += OnDevicesChanged;

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

        _calendarTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(5)
        };

        _calendarTimer.Tick += async (_, _) => await UpdateCalendarAsync();

        _devicesTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _devicesTimer.Tick += async (_, _) =>
        {
            if (RefreshDevicesCommand.CanExecute(null))
                await RefreshDevicesCommand.ExecuteAsync(null);
        };

        _ = UpdateCalendarAsync();
        _calendarTimer.Start();
        _ = UpdateWeatherAsync();
        _weatherTimer.Start();
        _ = RefreshDevicesCommand.ExecuteAsync(null);
        _devicesTimer.Start();
    }

    private void UpdateClock()
    {
        var now = _clockService.GetCurrentTime();

        Time = now.ToString("HH:mm:ss");
        Date = now.ToString("dddd, dd MMMM");
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

    private async Task UpdateCalendarAsync()
    {
        if (_isUpdatingCalendar || _isDisposed)
        {
            return;
        }

        _isUpdatingCalendar = true;

        try
        {
            CalendarEvents = await _calendarService.GetUpcomingEventsAsync(
                _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (_isDisposed)
        {
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Unable to update calendar: {exception.Message}");
        }
        finally
        {
            _isUpdatingCalendar = false;
        }
    }

    partial void OnCalendarEventsChanged(IReadOnlyList<CalendarEventModel> value)
    {
        OnPropertyChanged(nameof(HasNoCalendarEvents));
    }

    [RelayCommand]
    private async Task RefreshDevicesAsync()
    {
        if (_isDisposed || _isUpdatingDevices) return;
        _isUpdatingDevices = true;
        _deviceLoadStatus = "Loading devices…";
        OnPropertyChanged(nameof(PlugAvailability));
        try
        {
            var devices = await _smartDeviceService.GetDevicesAsync(_cancellationTokenSource.Token);
            if (_isDisposed) return;

            // Keep existing view models so their in-flight power commands remain valid.
            foreach (var device in SmartDevices.Where(d => !devices.Any(m => m.Id == d.Id)).ToArray())
                SmartDevices.Remove(device);
            foreach (var model in devices)
            {
                var existing = SmartDevices.FirstOrDefault(d => d.Id == model.Id);
                if (existing != null)
                    existing.UpdateState(model);
                else
                    SmartDevices.Add(new SmartDeviceViewModel(model, _smartDeviceService));
            }
            _deviceLoadStatus = null;
        }
        catch (OperationCanceledException) when (_isDisposed) { }
        catch (Exception exception)
        {
            Debug.WriteLine($"Unable to load smart devices: {exception.Message}");
            if (!_isDisposed) _deviceLoadStatus = "Devices unavailable · Refresh to retry";
        }
        finally
        {
            _isUpdatingDevices = false;
            if (!_isDisposed)
            {
                OnPropertyChanged(nameof(PlugAvailability));
                UpdateOverviewLight();
            }
        }
    }

    private void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Reconcile subscriptions for additions, removals and Collection.Clear().
        foreach (var device in _trackedDevices.Where(d => !SmartDevices.Contains(d)).ToArray())
        {
            device.PropertyChanged -= OnPlugChanged;
            device.Dispose();
            _trackedDevices.Remove(device);
        }
        foreach (var device in SmartDevices)
            if (_trackedDevices.Add(device)) device.PropertyChanged += OnPlugChanged;

        OnPropertyChanged(nameof(HasNoSmartPlugs));
        OnPropertyChanged(nameof(PlugSummary));
        OnPropertyChanged(nameof(PlugAvailability));
        UpdateOverviewLight();
    }

    private void UpdateOverviewLight()
    {
        if (_isUpdatingDevices || _isDisposed) return;
        // Keep the same light through renames and temporary outages.
        if (OverviewLight != null && SmartDevices.Contains(OverviewLight)) return;

        var lights = SmartDevices.Where(device => device.SupportsBrightness
            || device.SupportsColour || device.SupportsColourTemperature);
        OverviewLight = lights.FirstOrDefault(device =>
            string.Equals(device.Name, "Bedside Lamp", StringComparison.OrdinalIgnoreCase))
            ?? lights.FirstOrDefault();
    }

    private void OnPlugChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SmartDeviceViewModel.Capabilities))
            UpdateOverviewLight();
        if (e.PropertyName is nameof(SmartDeviceViewModel.HasState) or nameof(SmartDeviceViewModel.IsOn))
        {
            OnPropertyChanged(nameof(PlugSummary));
            OnPropertyChanged(nameof(PlugAvailability));
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
        _calendarTimer.Stop();
        _devicesTimer.Stop();
        SmartDevices.CollectionChanged -= OnDevicesChanged;
        foreach (var plug in _trackedDevices)
        {
            plug.PropertyChanged -= OnPlugChanged;
            plug.Dispose();
        }
        _trackedDevices.Clear();
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}

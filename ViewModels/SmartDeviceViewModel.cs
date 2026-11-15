
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalDashboard.Models;
using DigitalDashboard.Services;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalDashboard.ViewModels;

public partial class SmartDeviceViewModel : ViewModelBase, IDisposable
{
    private readonly ISmartDeviceService _smartDeviceService;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly DispatcherTimer _refreshTimer;
    private bool _isDisposed;

    public string Id { get; }

    public string Type { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupportsBrightness))]
    [NotifyPropertyChangedFor(nameof(SupportsColour))]
    [NotifyPropertyChangedFor(nameof(SupportsEnergyMonitoring))]
    private SmartDeviceCapabilities? _capabilities;

    public bool SupportsBrightness => Capabilities?.Brightness == true;

    public bool SupportsColour => Capabilities?.Colour == true;

    public bool SupportsEnergyMonitoring => Capabilities?.EnergyMonitoring == true;

    [ObservableProperty]
    private string _name = "Smart Device";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PowerButtonText))]
    private bool _isOn;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyPropertyChangedFor(nameof(PowerButtonText))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleCommand))]
    [NotifyPropertyChangedFor(nameof(PowerButtonText))]
    private bool _hasState;

    [ObservableProperty]
    private string _status = "Connecting…";

    public string PowerButtonText => IsLoading ? "Please wait…" :
        !HasState ? "Power unavailable" : IsOn ? "Turn off" : "Turn on";

    public SmartDeviceViewModel(
        SmartDeviceModel model,
        ISmartDeviceService smartDeviceService)
    {
        _smartDeviceService = smartDeviceService;

        Id = model.Id;
        Type = model.Type;

        UpdateState(model);

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };

        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        _refreshTimer.Start();
    }

    public void UpdateState(SmartDeviceModel model)
    {
        if (_isDisposed || IsLoading)
        {
            return;
        }

        Name = string.IsNullOrWhiteSpace(model.Name)
            ? "Smart Device"
            : model.Name;

        IsOn = model.IsOn ?? false;
        HasState = model.Available && model.IsOn.HasValue;
        Capabilities = model.Capabilities;

        Status = !HasState
            ? "Unavailable · Retry"
            : IsOn ? "Power on" : "Power off";
    }

    private bool CanRefresh() => !IsLoading && !_isDisposed;

    private bool CanToggle() => CanRefresh() && HasState;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private Task RefreshAsync() => RunAsync(null);

    [RelayCommand(CanExecute = nameof(CanToggle))]
    private Task ToggleAsync() => CanToggle() ? RunAsync(!IsOn) : Task.CompletedTask;

    private async Task RunAsync(bool? desiredPower)
    {
        if (!CanRefresh())
        {
            return;
        }

        IsLoading = true;
        Status = desiredPower.HasValue ? "Updating…" : "Refreshing…";

        try
        {
            if (desiredPower.HasValue)
            {
                await _smartDeviceService.SetPowerAsync(
                    Id,
                    desiredPower.Value,
                    _lifetime.Token);
            }

            var device = await _smartDeviceService.GetStateAsync(
                Id,
                _lifetime.Token);

            if (_isDisposed)
            {
                return;
            }

            Name = string.IsNullOrWhiteSpace(device.Name)
                ? "Smart Device"
                : device.Name;

            IsOn = device.IsOn ?? false;
            HasState = device.Available && device.IsOn.HasValue;
            Capabilities = device.Capabilities;

            Status = !HasState
                ? "Unavailable · Retry"
                : IsOn ? "Power on" : "Power off";
        }
        catch (OperationCanceledException) when (_isDisposed)
        {
        }
        catch (Exception exception) when (exception is HttpRequestException
            or OperationCanceledException or InvalidOperationException or JsonException)
        {
            Debug.WriteLine($"Smart device {Id} request failed: {exception.Message}");

            if (!_isDisposed)
            {
                HasState = false;
                Status = "Unavailable · Retry";
            }
        }
        finally
        {
            if (!_isDisposed)
            {
                IsLoading = false;
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

        _refreshTimer.Stop();
        _lifetime.Cancel();
        _lifetime.Dispose();

        ToggleCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
    }
}

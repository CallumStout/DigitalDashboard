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
    private readonly DispatcherTimer _brightnessTimer;
    private int? _pendingBrightness;
    private bool _isApplyingState;
    private bool _isDisposed;

    public string Id { get; }

    public string Type { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SupportsBrightness))]
    [NotifyPropertyChangedFor(nameof(SupportsColour))]
    [NotifyPropertyChangedFor(nameof(SupportsEnergyMonitoring))]
    [NotifyPropertyChangedFor(nameof(SupportsColourTemperature))]
    [NotifyPropertyChangedFor(nameof(SupportsPresets))]
    [NotifyPropertyChangedFor(nameof(CanAdjustBrightness))]
    [NotifyCanExecuteChangedFor(nameof(ApplyPresetCommand))]
    private SmartDeviceCapabilities? _capabilities;

    public bool SupportsBrightness => Capabilities?.Brightness == true;

    public bool SupportsColour => Capabilities?.Colour == true;

    public bool SupportsEnergyMonitoring => Capabilities?.EnergyMonitoring == true;

    public bool SupportsColourTemperature => Capabilities?.ColourTemperature == true;

    public bool SupportsPresets => SupportsBrightness && SupportsColourTemperature;

    public bool CanAdjustBrightness => CanToggle() && SupportsBrightness;

    // This is a requested level: the current bridge does not report brightness.
    [ObservableProperty]
    private int _brightness = 50;

    [ObservableProperty]
    private string _name = "Smart Device";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PowerButtonText))]
    private bool _isOn;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyPresetCommand))]
    [NotifyPropertyChangedFor(nameof(CanAdjustBrightness))]
    [NotifyPropertyChangedFor(nameof(PowerButtonText))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyPresetCommand))]
    [NotifyPropertyChangedFor(nameof(CanAdjustBrightness))]
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

        _brightnessTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _brightnessTimer.Tick += async (_, _) => await ApplyPendingBrightnessAsync();

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
        if (_isDisposed || IsLoading || _pendingBrightness.HasValue)
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

    private bool CanApplyPreset(string? preset) => CanToggle() && SupportsPresets
        && preset is "relax" or "bright";

    partial void OnBrightnessChanged(int value)
    {
        if (_isApplyingState || !CanAdjustBrightness) return;
        _pendingBrightness = Math.Clamp(value, 1, 100);
        _brightnessTimer.Stop();
        _brightnessTimer.Start();
    }

    private async Task ApplyPendingBrightnessAsync()
    {
        if (IsLoading) return;
        var brightness = _pendingBrightness;
        CancelPendingBrightness();
        if (brightness.HasValue && CanAdjustBrightness)
            await RunAsync(null, brightness.Value);
    }

    private void CancelPendingBrightness()
    {
        _brightnessTimer.Stop();
        _pendingBrightness = null;
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private Task RefreshAsync() => _pendingBrightness.HasValue ? Task.CompletedTask : RunAsync(null);

    [RelayCommand(CanExecute = nameof(CanToggle))]
    private Task ToggleAsync()
    {
        if (!CanToggle()) return Task.CompletedTask;
        CancelPendingBrightness();
        return RunAsync(!IsOn);
    }

    [RelayCommand(CanExecute = nameof(CanApplyPreset))]
    private Task ApplyPresetAsync(string? preset)
    {
        if (!CanApplyPreset(preset)) return Task.CompletedTask;
        CancelPendingBrightness();
        return RunAsync(null, preset: preset);
    }

    private async Task RunAsync(bool? desiredPower, int? desiredBrightness = null, string? preset = null)
    {
        if (!CanRefresh())
        {
            return;
        }

        IsLoading = true;
        Status = desiredPower.HasValue || desiredBrightness.HasValue || preset != null ? "Updating…" : "Refreshing…";

        try
        {
            if (desiredPower.HasValue)
            {
                await _smartDeviceService.SetPowerAsync(
                    Id,
                    desiredPower.Value,
                    _lifetime.Token);
            }
            else if (desiredBrightness.HasValue)
            {
                await _smartDeviceService.SetBrightnessAsync(Id, desiredBrightness.Value, _lifetime.Token);
            }
            else if (preset != null)
            {
                await _smartDeviceService.SetPresetAsync(Id, preset, _lifetime.Token);
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

            // Update the target after a successful preset without issuing another write.
            if (preset != null)
            {
                _isApplyingState = true;
                try { Brightness = preset == "relax" ? 30 : 100; }
                finally { _isApplyingState = false; }
            }

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
        CancelPendingBrightness();
        _lifetime.Cancel();
        _lifetime.Dispose();

        ToggleCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        ApplyPresetCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanAdjustBrightness));
    }
}

using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalDashboard.Services;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalDashboard.ViewModels;

public partial class SmartPlugViewModel : ViewModelBase, IDisposable
{
    private readonly ISmartPlugService _smartPlugService;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly DispatcherTimer _refreshTimer;
    private bool _isDisposed;

    [ObservableProperty]
    private string _name = "Smart Plug";

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

    public SmartPlugViewModel(ISmartPlugService smartPlugService)
    {
        _smartPlugService = smartPlugService;
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        _refreshTimer.Start();
        _ = RefreshAsync();
    }

    private bool CanRefresh() => !IsLoading && !_isDisposed;
    private bool CanToggle() => CanRefresh() && HasState;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private Task RefreshAsync() => RunAsync(null);

    [RelayCommand(CanExecute = nameof(CanToggle))]
    private Task ToggleAsync() => CanToggle() ? RunAsync(!IsOn) : Task.CompletedTask;

    // Serialize reads and writes so a stale refresh cannot overwrite a power change.
    private async Task RunAsync(bool? desiredPower)
    {
        if (!CanRefresh()) return;
        IsLoading = true;
        Status = desiredPower.HasValue ? "Updating…" : "Refreshing…";
        try
        {
            if (desiredPower.HasValue)
                await _smartPlugService.SetPowerAsync(desiredPower.Value, _lifetime.Token);

            var plug = await _smartPlugService.GetStateAsync(_lifetime.Token);
            if (_isDisposed) return;
            Name = string.IsNullOrWhiteSpace(plug.Name) ? "Smart Plug" : plug.Name;
            IsOn = plug.IsOn;
            HasState = true;
            Status = IsOn ? "Power on" : "Power off";
        }
        catch (OperationCanceledException) when (_isDisposed) { }
        catch (Exception exception) when (exception is HttpRequestException
            or OperationCanceledException or InvalidOperationException or JsonException)
        {
            Debug.WriteLine($"Smart plug request failed: {exception.Message}");
            if (!_isDisposed)
            {
                HasState = false;
                Status = "Unavailable · Retry";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _refreshTimer.Stop();
        _lifetime.Cancel();
        _lifetime.Dispose();
        ToggleCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
    }
}

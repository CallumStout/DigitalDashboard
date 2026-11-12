
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DigitalClock.Services;
using System;

namespace DigitalClock.ViewModels;

public partial class ClockViewModel : ViewModelBase
{
    private readonly IClockService _clockService;
    private readonly DispatcherTimer _timer;

    [ObservableProperty]
    private string _time = string.Empty;

    [ObservableProperty]
    private string _date = string.Empty;

    public ClockViewModel(IClockService clockService)
    {
        _clockService = clockService;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        _timer.Tick += (_, _) => UpdateClock();

        UpdateClock();
        _timer.Start();
    }

    private void UpdateClock()
    { 
        var now = _clockService.GetCurrentTime();

        Time = now.ToString("HH:mm:ss");
        Date = now.ToString("dddd, dd MMMM yyyy");
    }
}

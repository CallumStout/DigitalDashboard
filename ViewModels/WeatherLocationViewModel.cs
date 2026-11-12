using CommunityToolkit.Mvvm.ComponentModel;
using DigitalClock.Models;

namespace DigitalClock.ViewModels;

public partial class WeatherLocationViewModel : ViewModelBase
{
    public WeatherLocation Location { get; }

    public string Name => Location.Name;

    [ObservableProperty]
    private string _summary = "Loading weather...";

    public WeatherLocationViewModel(WeatherLocation location)
    {
        Location = location;
    }
}

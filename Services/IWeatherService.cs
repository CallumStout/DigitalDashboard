using DigitalClock.Models;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalClock.Services;

public interface IWeatherService
{
    Task<Weather> GetCurrentWeatherAsync(WeatherLocation location, CancellationToken cancellationToken = default);
}

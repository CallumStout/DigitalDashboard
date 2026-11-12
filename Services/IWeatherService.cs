using DigitalDashboard.Models;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalDashboard.Services;

public interface IWeatherService
{
    Task<Weather> GetCurrentWeatherAsync(WeatherLocation location, CancellationToken cancellationToken = default);
}

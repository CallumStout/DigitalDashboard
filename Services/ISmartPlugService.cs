
using DigitalDashboard.Models;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalDashboard.Services;

public interface ISmartPlugService
{
    Task<SmartPlugModel> GetStateAsync(CancellationToken cancellationToken = default);

    Task SetPowerAsync(bool enabled, CancellationToken cancellationToken = default);
}

using DigitalDashboard.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalDashboard.Services;

public interface ISmartDeviceService
{
    Task<List<SmartDeviceModel>> GetDevicesAsync(CancellationToken cancellationToken = default);

    Task<SmartDeviceModel> GetStateAsync(string deviceId, CancellationToken cancellationToken = default);

    Task SetPowerAsync(string deviceId, bool enabled, CancellationToken cancellationToken = default);
}
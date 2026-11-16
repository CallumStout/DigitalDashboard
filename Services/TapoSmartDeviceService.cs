using DigitalDashboard.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalDashboard.Services;

public class TapoSmartDeviceService : ISmartDeviceService
{
    private readonly HttpClient _httpClient;

    public TapoSmartDeviceService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<SmartDeviceModel>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        var devices = await _httpClient.GetFromJsonAsync<List<SmartDeviceModel>>(
            "api/devices",
            cancellationToken);

        return devices ?? throw new InvalidOperationException("No smart devices returned.");
    }

    public async Task<SmartDeviceModel> GetStateAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        var device = await _httpClient.GetFromJsonAsync<SmartDeviceModel>(
            $"api/devices/{Uri.EscapeDataString(deviceId)}",
            cancellationToken);

        return device ?? throw new InvalidOperationException("No smart device data returned.");
    }

    public async Task SetPowerAsync(string deviceId, bool enabled, CancellationToken cancellationToken = default)
    {
        var endpoint = $"api/devices/{Uri.EscapeDataString(deviceId)}/power?enabled={enabled.ToString().ToLowerInvariant()}";

        using var response = await _httpClient.PostAsync(
            endpoint,
            null,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task SetBrightnessAsync(string deviceId, int brightness, CancellationToken cancellationToken = default)
    {
        if (brightness is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(brightness));

        var endpoint = $"api/devices/{Uri.EscapeDataString(deviceId)}/brightness?brightness={brightness}";
        using var response = await _httpClient.PostAsync(endpoint, null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetPresetAsync(string deviceId, string preset, CancellationToken cancellationToken = default)
    {
        if (preset is not ("relax" or "bright"))
            throw new ArgumentException("Unknown lighting preset.", nameof(preset));

        var endpoint = $"api/devices/{Uri.EscapeDataString(deviceId)}/preset?preset={preset}";
        using var response = await _httpClient.PostAsync(endpoint, null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

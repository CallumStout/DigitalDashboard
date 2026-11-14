using DigitalDashboard.Models;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalDashboard.Services;

public class TapoSmartPlugService : ISmartPlugService
{
    private readonly HttpClient _httpClient;

    public TapoSmartPlugService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<SmartPlugModel> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var plug = await _httpClient.GetFromJsonAsync<SmartPlugModel>(
            "api/plug",
            cancellationToken);

        return plug ?? throw new InvalidOperationException("Smart plug returned no data.");
    }

    public async Task SetPowerAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var endpoint = enabled ? "api/plug/on" : "api/plug/off";

        using var response = await _httpClient.PostAsync(
            endpoint,
            null,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}

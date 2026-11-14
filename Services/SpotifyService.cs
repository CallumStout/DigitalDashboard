using SpotifyAPI.Web;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalDashboard.Services;

public class SpotifyService : ISpotifyService
{
    private readonly SpotifyAuthenticationService _authenticationService;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private SpotifyClient? _client;

    public SpotifyService(SpotifyAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    private async Task<SpotifyClient> GetClientAsync()
    {
        if (_client != null)
        {
            return _client;
        }

        await _initializationLock.WaitAsync();

        try
        {
            if (_client == null)
            {
                _client = await _authenticationService.CreateClientAsync();
            }

            return _client;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<CurrentlyPlayingContext?> GetCurrentlyPlayingAsync()
    {
        var client = await GetClientAsync();

        return await client.Player.GetCurrentPlayback();
    }

    public async Task PauseAsync()
    {
        var client = await GetClientAsync();

        await client.Player.PausePlayback();
    }

    public async Task ResumeAsync()
    {
        var client = await GetClientAsync();

        await client.Player.ResumePlayback();
    }

    public async Task NextTrackAsync()
    {
        var client = await GetClientAsync();

        await client.Player.SkipNext();
    }

    public async Task PreviousTrackAsync()
    {
        var client = await GetClientAsync();

        await client.Player.SkipPrevious();
    }
}

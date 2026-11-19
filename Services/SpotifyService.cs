using SpotifyAPI.Web;
using System;
using System.Linq;
using DigitalDashboard.Models;
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

    public async Task<SpotifyLibraryPage> GetLibraryAsync(string category, int offset, string query)
    {
        var client = await GetClientAsync();
        SpotifyLibraryItem Map(FullTrack t) => new(t.Name, string.Join(", ", t.Artists.Select(a => a.Name)), t.Uri, ArtworkUrl: t.Album?.Images?.OrderBy(i => Math.Abs(i.Width - 160)).FirstOrDefault()?.Url);
        switch (category)
        {
            case "Liked Songs":
                var liked = await client.Library.GetTracks(new LibraryTracksRequest { Limit = 50, Offset = offset });
                return new(liked.Items?.Where(t => t.Track != null).Select(t => Map(t.Track)).ToArray() ?? [], liked.Next != null);
            case "Playlists":
                var playlists = await client.Playlists.CurrentUsers(new PlaylistCurrentUsersRequest { Limit = 50, Offset = offset });
                return new(playlists.Items?.Where(p => p != null && !string.IsNullOrWhiteSpace(p.Uri)).Select(p => new SpotifyLibraryItem(p.Name ?? "Untitled playlist", p.Owner?.DisplayName ?? "Playlist", p.Uri!, true, p.Images?.OrderBy(i => Math.Abs(i.Width - 160)).FirstOrDefault()?.Url)).ToArray() ?? [], playlists.Next != null);
            case "Recent":
                var recent = await client.Player.GetRecentlyPlayed(new PlayerRecentlyPlayedRequest { Limit = 50 });
                return new(recent.Items?.Where(p => p.Track != null).Select(p => Map(p.Track)).DistinctBy(t => t.Uri).ToArray() ?? [], false);
            case "Top Tracks":
                var top = await client.Personalization.GetTopTracks(new PersonalizationTopRequest { Limit = 50, Offset = offset });
                return new(top.Items?.Select(Map).ToArray() ?? [], top.Next != null);
            case "Search":
                var search = await client.Search.Item(new SearchRequest(SearchRequest.Types.Track, query) { Limit = 10, Offset = offset });
                return new(search.Tracks?.Items?.Select(Map).ToArray() ?? [], search.Tracks?.Next != null);
            default:
                throw new ArgumentException("Unknown Spotify category.", nameof(category));
        }
    }

    public async Task PlayAsync(SpotifyLibraryItem item)
    {
        var client = await GetClientAsync();
        await client.Player.ResumePlayback(item.IsPlaylist
            ? new PlayerResumePlaybackRequest { ContextUri = item.Uri }
            : new PlayerResumePlaybackRequest { Uris = new[] { item.Uri } });
    }

    public async Task SetShuffleAsync(bool enabled)
    {
        var client = await GetClientAsync();
        await client.Player.SetShuffle(new PlayerShuffleRequest(enabled));
    }

    public async Task SetRepeatAsync(string mode)
    {
        var client = await GetClientAsync();
        var state = mode == "track" ? PlayerSetRepeatRequest.State.Track
            : mode == "context" ? PlayerSetRepeatRequest.State.Context : PlayerSetRepeatRequest.State.Off;
        await client.Player.SetRepeat(new PlayerSetRepeatRequest(state));
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

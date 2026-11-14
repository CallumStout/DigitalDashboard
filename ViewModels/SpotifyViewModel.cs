using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpotifyAPI.Web;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalDashboard.ViewModels;

public partial class SpotifyViewModel : ObservableObject, IDisposable
{
    private readonly ISpotifyService _spotifyService;
    private readonly HttpClient _httpClient;
    private readonly DispatcherTimer _timer;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _requests = new(1, 1);
    private bool _isRefreshing;
    private readonly CancellationToken _token;
    private string? _artworkUrl;
    private DateTime _artworkRetryAfter;
    private int _artworkVersion;
    private bool _disposed;

    [ObservableProperty] private string _trackName = "Connecting…";
    [ObservableProperty] private string _artistName = "Spotify";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasArtwork))]
    private Bitmap? _albumArtwork;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackIcon))]
    [NotifyPropertyChangedFor(nameof(PlaybackLabel))]
    private bool _isPlaying;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TogglePlaybackCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextTrackCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousTrackCommand))]
    private bool _isLoading;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TogglePlaybackCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextTrackCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousTrackCommand))]
    private bool _hasPlayback;
    [ObservableProperty] private string _status = "Connecting";

    public bool HasArtwork => AlbumArtwork != null;
    public string PlaybackIcon => IsPlaying ? "Ⅱ" : "▶";
    public string PlaybackLabel => IsPlaying ? "Pause Spotify" : "Play Spotify";

    public SpotifyViewModel(ISpotifyService spotifyService, HttpClient httpClient)
    {
        _spotifyService = spotifyService;
        _httpClient = httpClient;
        _token = _lifetime.Token;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();

        // Start after the window/view has been constructed, on Avalonia's UI thread.
        Dispatcher.UIThread.Post(async () => await RefreshAsync());
    }

    private bool CanControl() => !_disposed && !IsLoading && HasPlayback;

    [RelayCommand]
    private Task RefreshAsync() => UpdateAsync();
    
    [RelayCommand(CanExecute = nameof(CanControl))]
    private Task TogglePlaybackAsync() => CanControl()
        ? UpdateAsync(IsPlaying ? _spotifyService.PauseAsync : _spotifyService.ResumeAsync)
        : Task.CompletedTask;
    
    [RelayCommand(CanExecute = nameof(CanControl))]
    private Task NextTrackAsync() => CanControl() ? UpdateAsync(_spotifyService.NextTrackAsync) : Task.CompletedTask;
    
    [RelayCommand(CanExecute = nameof(CanControl))]
    private Task PreviousTrackAsync() => CanControl() ? UpdateAsync(_spotifyService.PreviousTrackAsync) : Task.CompletedTask;

    // Timer ticks and bound commands run on the UI thread. Await preserves that
    // context, so there is no worker loop or per-property dispatcher queue.
    private async Task UpdateAsync(Func<Task>? command = null)
    {
        if (_disposed || IsLoading || (command == null && _isRefreshing))
            return;

        // Poll quietly. A user command can wait for the current read without
        // being dropped, while only user actions change button appearance.
        if (command == null) _isRefreshing = true;
        
        else IsLoading = true;
        
        var acquired = false;
        try
        {
            await _requests.WaitAsync(_token);
            acquired = true;
            if (command != null)
            {
                Status = "Updating…";
                await command().WaitAsync(_token);
            }

            var playback = await _spotifyService.GetCurrentlyPlayingAsync().WaitAsync(_token);
            
            if (_disposed) 
                return;

            var track = playback?.Item as FullTrack;

            TrackName = track?.Name ?? "Nothing playing";
            ArtistName = track == null ? "Open Spotify on a device" :
                string.Join(", ", track.Artists?.Select(artist => artist.Name) ?? Enumerable.Empty<string>());
            HasPlayback = track != null;
            IsPlaying = track != null && playback!.IsPlaying;
            Status = track == null ? "Idle" : IsPlaying ? "Playing" : "Paused";

            var images = track?.Album?.Images;
            var url = images?.OrderByDescending(image => image.Width).FirstOrDefault()?.Url
                ?? images?.FirstOrDefault()?.Url;

            // Cover downloads must not keep the transport buttons disabled.
            _ = UpdateArtworkAsync(url);
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
        
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Spotify request failed: {ex}");

            if (_disposed) 
                return;

            HasPlayback = false;
            IsPlaying = false;
            TrackName = "Spotify unavailable";
            ArtistName = "Retrying automatically";
            Status = "Offline";
            ReplaceArtwork(null);
            _artworkUrl = null;
            _artworkVersion++;
        }
        finally
        {
            if (acquired) _requests.Release();
            if (command == null) _isRefreshing = false;
            else if (!_disposed) IsLoading = false;
        }
    }

    private async Task UpdateArtworkAsync(string? url)
    {
        if (url == _artworkUrl && (HasArtwork || DateTime.UtcNow < _artworkRetryAfter)) 
            return;
     
        _artworkUrl = url;
        
        var version = ++_artworkVersion;
        
        _artworkRetryAfter = DateTime.UtcNow.AddMinutes(1);
        
        ReplaceArtwork(null);
        
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) return;

        try
        {
            // Buffer the complete response before decoding. Do not pass a live
            // HTTP stream to Skia's scaled decoder on a background thread.
            var bytes = await _httpClient.GetByteArrayAsync(uri, _token);
        
            if (_disposed || version != _artworkVersion) return;
            
            using var stream = new MemoryStream(bytes, writable: false);
            
            ReplaceArtwork(new Bitmap(stream));
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
        
        catch (Exception ex)
        {
            // Keep playback working and avoid retrying a bad cover every five seconds.
            System.Diagnostics.Debug.WriteLine($"Spotify artwork failed: {ex}");
        }
    }

    private void ReplaceArtwork(Bitmap? bitmap)
    {
        var previous = AlbumArtwork;
        AlbumArtwork = bitmap;
        previous?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) 
            return;
        
        _disposed = true;
        _timer.Stop();
        _lifetime.Cancel();
        _lifetime.Dispose();
        ReplaceArtwork(null);
        TogglePlaybackCommand.NotifyCanExecuteChanged();
        NextTrackCommand.NotifyCanExecuteChanged();
        PreviousTrackCommand.NotifyCanExecuteChanged();
    }
}

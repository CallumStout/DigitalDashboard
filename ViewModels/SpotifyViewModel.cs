using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using SpotifyAPI.Web;
using DigitalDashboard.Models;
using System.Collections.ObjectModel;
using System;
using System.IO;
using System.Diagnostics;
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
    private readonly DispatcherTimer _progressTimer;
    private readonly Stopwatch _progressClock = new();
    private int _progressAnchorMs;
    private int _durationMs;
    private CancellationTokenSource? _libraryArtworkLifetime;
    private readonly SemaphoreSlim _coverRequests = new(4, 4);
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
    [NotifyCanExecuteChangedFor(nameof(ToggleShuffleCommand))]
    [NotifyCanExecuteChangedFor(nameof(CycleRepeatCommand))]
    [NotifyCanExecuteChangedFor(nameof(TogglePlaybackCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextTrackCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousTrackCommand))]
    private bool _isLoading;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleShuffleCommand))]
    [NotifyCanExecuteChangedFor(nameof(CycleRepeatCommand))]
    [NotifyCanExecuteChangedFor(nameof(TogglePlaybackCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextTrackCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousTrackCommand))]
    private bool _hasPlayback;
    [ObservableProperty] private string _status = "Connecting";

    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _deviceLabel = "Open Spotify on your listening device";
    [ObservableProperty] private string _playbackError = "";
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string _elapsed = "0:00";
    [ObservableProperty] private string _duration = "0:00";
    [ObservableProperty] private bool _shuffleEnabled;
    [ObservableProperty] private string _repeatMode = "off";
    [ObservableProperty] private bool _isBrowsing;
    [ObservableProperty] private bool _isLibraryLoading;
    [ObservableProperty] private bool _hasMore;
    [ObservableProperty] private bool _libraryFailed;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CategoryDescription))]
    private string _category = "Liked Songs";
    [ObservableProperty] private string _libraryStatus = "";
    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private bool _isSearch;
    private int _libraryOffset;
    private string _submittedQuery = "";
    public ObservableCollection<SpotifyLibraryItemViewModel> LibraryItems { get; } = [];
    public string CategoryDescription => Category switch
    {
        "Liked Songs" => "Your favourites, always within reach.",
        "Playlists" => "Find the soundtrack for right now.",
        "Recent" => "Pick up where you left off.",
        "Top Tracks" => "The songs you keep coming back to.",
        _ => "Find your next favourite."
    };

    private void ClearLibrary()
    {
        _libraryArtworkLifetime?.Cancel();
        _libraryArtworkLifetime?.Dispose();
        _libraryArtworkLifetime = CancellationTokenSource.CreateLinkedTokenSource(_token);
        foreach (var item in LibraryItems) item.Dispose();
        LibraryItems.Clear();
    }

    private async Task LoadLibraryArtworkAsync(SpotifyLibraryItemViewModel item, CancellationToken token)
    {
        if (!Uri.TryCreate(item.Item.ArtworkUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return;
        var acquired = false;
        try
        {
            await _coverRequests.WaitAsync(token);
            acquired = true;
            var bytes = await _httpClient.GetByteArrayAsync(uri, token);
            if (token.IsCancellationRequested || _disposed) return;
            using var stream = new MemoryStream(bytes, writable: false);
            item.Artwork = Bitmap.DecodeToWidth(stream, 160);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { Debug.WriteLine($"Spotify library cover failed: {ex.Message}"); }
        finally { if (acquired) _coverRequests.Release(); }
    }

    [RelayCommand]
    private async Task BrowseAsync(string category)
    {
        if (_disposed || IsLibraryLoading) return;
        Category = category;
        IsSearch = category == "Search";
        _submittedQuery = "";
        IsBrowsing = true;
        ClearLibrary();
        _libraryOffset = 0;
        HasMore = false;
        LibraryFailed = false;
        LibraryStatus = IsSearch ? "Enter a song or artist to search" : "";
        if (!IsSearch) await LoadLibraryAsync();
    }

    [RelayCommand]
    private void CloseLibrary() => IsBrowsing = false;

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (_disposed || IsLibraryLoading || string.IsNullOrWhiteSpace(SearchQuery)) return;
        _submittedQuery = SearchQuery.Trim();
        ClearLibrary();
        _libraryOffset = 0;
        await LoadLibraryAsync();
    }

    [RelayCommand]
    private async Task LoadLibraryAsync()
    {
        if (_disposed || IsLibraryLoading || (IsSearch && string.IsNullOrWhiteSpace(_submittedQuery))) return;
        IsLibraryLoading = true;
        LibraryFailed = false;
        LibraryStatus = "Loading…";
        try
        {
            var page = await _spotifyService.GetLibraryAsync(Category, _libraryOffset, _submittedQuery).WaitAsync(_token);
            if (_disposed) return;
            _libraryArtworkLifetime ??= CancellationTokenSource.CreateLinkedTokenSource(_token);
            foreach (var item in page.Items)
            {
                var row = new SpotifyLibraryItemViewModel(item);
                LibraryItems.Add(row);
                _ = LoadLibraryArtworkAsync(row, _libraryArtworkLifetime.Token);
            }
            _libraryOffset += IsSearch ? 10 : 50;
            HasMore = page.HasMore;
            LibraryStatus = LibraryItems.Count == 0 ? "No music found" : $"{LibraryItems.Count} items";
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            if (!_disposed)
            {
                LibraryFailed = true;
                LibraryStatus = "Unable to load music. Tap Retry.";
            }
        }
        finally { if (!_disposed) IsLibraryLoading = false; }
    }

    [RelayCommand]
    private Task PlayItemAsync(SpotifyLibraryItem item)
    {
        if (item == null || _disposed || IsLoading) return Task.CompletedTask;
        IsBrowsing = false;
        return UpdateAsync(() => _spotifyService.PlayAsync(item));
    }

    [RelayCommand(CanExecute = nameof(CanControl))]
    private Task ToggleShuffleAsync() => UpdateAsync(() => _spotifyService.SetShuffleAsync(!ShuffleEnabled));

    [RelayCommand(CanExecute = nameof(CanControl))]
    private Task CycleRepeatAsync() => UpdateAsync(() => _spotifyService.SetRepeatAsync(
        RepeatMode == "off" ? "context" : RepeatMode == "context" ? "track" : "off"));

    private static string FormatTime(int milliseconds) =>
        TimeSpan.FromMilliseconds(Math.Max(0, milliseconds)).ToString(@"m\:ss");

    public bool HasArtwork => AlbumArtwork != null;
    public MaterialIconKind PlaybackIcon => IsPlaying ? MaterialIconKind.Pause : MaterialIconKind.Play;
    public string PlaybackLabel => IsPlaying ? "Pause Spotify" : "Play Spotify";

    public SpotifyViewModel(ISpotifyService spotifyService, HttpClient httpClient)
    {
        _spotifyService = spotifyService;
        _httpClient = httpClient;
        _token = _lifetime.Token;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _progressTimer.Tick += (_, _) => UpdateProgress();
        _progressTimer.Start();

        // Start after the window/view has been constructed, on Avalonia's UI thread.
        Dispatcher.UIThread.Post(async () => await RefreshAsync());
    }

    private void UpdateProgress()
    {
        var position = Math.Clamp(_progressAnchorMs + (IsPlaying ? _progressClock.Elapsed.TotalMilliseconds : 0), 0, _durationMs);
        ProgressPercent = _durationMs > 0 ? 100.0 * position / _durationMs : 0;
        Elapsed = FormatTime((int)position);
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
                PlaybackError = "";
                await command().WaitAsync(_token);
            }

            var playback = await _spotifyService.GetCurrentlyPlayingAsync().WaitAsync(_token);
            
            if (_disposed) 
                return;

            var track = playback?.Item as FullTrack;

            TrackName = track?.Name ?? "Nothing playing";
            ArtistName = track == null ? "Open Spotify on a device" :
                string.Join(", ", track.Artists?.Select(artist => artist.Name) ?? Enumerable.Empty<string>());
            IsConnected = true;
            HasPlayback = playback?.Device != null && !playback.Device.IsRestricted;
            DeviceLabel = playback?.Device == null ? "Open Spotify on your listening device"
                : $"{(playback.IsPlaying ? "Playing" : "Connected")} on {playback.Device.Name}";
            _progressAnchorMs = playback?.ProgressMs ?? 0;
            _durationMs = track?.DurationMs ?? 0;
            _progressClock.Restart();
            Duration = FormatTime(track?.DurationMs ?? 0);

            ShuffleEnabled = playback?.ShuffleState ?? false;
            RepeatMode = playback?.RepeatState ?? "off";
            IsPlaying = track != null && playback!.IsPlaying;
            UpdateProgress();
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

            if (command != null)
            {
                PlaybackError = "Could not control Spotify. Start playback on your device and check Premium access.";
                return;
            }
            IsConnected = false;
            DeviceLabel = "Spotify unavailable · reconnecting";
            _progressClock.Reset();
            _progressAnchorMs = _durationMs = 0;
            ProgressPercent = 0;
            Elapsed = Duration = "0:00";
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
        _progressTimer.Stop();
        _progressClock.Stop();
        _libraryArtworkLifetime?.Cancel();
        _libraryArtworkLifetime?.Dispose();
        foreach (var item in LibraryItems) item.Dispose();
        LibraryItems.Clear();
        _lifetime.Cancel();
        _lifetime.Dispose();
        ReplaceArtwork(null);
        TogglePlaybackCommand.NotifyCanExecuteChanged();
        NextTrackCommand.NotifyCanExecuteChanged();
        PreviousTrackCommand.NotifyCanExecuteChanged();
        ToggleShuffleCommand.NotifyCanExecuteChanged();
        CycleRepeatCommand.NotifyCanExecuteChanged();
    }
}

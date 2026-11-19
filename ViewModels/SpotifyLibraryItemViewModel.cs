using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using DigitalDashboard.Models;
using System;

namespace DigitalDashboard.ViewModels;

public partial class SpotifyLibraryItemViewModel(SpotifyLibraryItem item) : ObservableObject, IDisposable
{
    public SpotifyLibraryItem Item { get; } = item;
    public string Name => Item.Name;
    public string Subtitle => Item.Subtitle;
    public string KindLabel => Item.IsPlaylist ? "PLAYLIST" : "TRACK";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasArtwork))]
    private Bitmap? _artwork;
    public bool HasArtwork => Artwork != null;
    public void Dispose()
    {
        var previous = Artwork;
        Artwork = null;
        previous?.Dispose();
    }
}

using System.Collections.Generic;

namespace DigitalDashboard.Models;

public record SpotifyLibraryItem(string Name, string Subtitle, string Uri, bool IsPlaylist = false);
public record SpotifyLibraryPage(IReadOnlyList<SpotifyLibraryItem> Items, bool HasMore);

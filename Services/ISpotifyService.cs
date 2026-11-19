using SpotifyAPI.Web;
using DigitalDashboard.Models;
using System.Threading.Tasks;

public interface ISpotifyService
{
    Task<CurrentlyPlayingContext?> GetCurrentlyPlayingAsync();

    Task<SpotifyLibraryPage> GetLibraryAsync(string category, int offset, string query);
    Task PlayAsync(SpotifyLibraryItem item);
    Task SetShuffleAsync(bool enabled);
    Task SetRepeatAsync(string mode);

    Task PauseAsync();

    Task ResumeAsync();

    Task NextTrackAsync();

    Task PreviousTrackAsync();
}
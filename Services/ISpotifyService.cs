using SpotifyAPI.Web;
using System.Threading.Tasks;

public interface ISpotifyService
{
    Task<CurrentlyPlayingContext?> GetCurrentlyPlayingAsync();

    Task PauseAsync();

    Task ResumeAsync();

    Task NextTrackAsync();

    Task PreviousTrackAsync();
}
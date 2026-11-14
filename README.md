# DigitalDashboard

A fullscreen Avalonia dashboard with a live clock, weather cards, and upcoming
Google Calendar events. Built with .NET 10.

The dark home overview is designed for 7-inch landscape displays at 800×480 and
1024×600. A persistent left navigation rail sits beside three flexible columns:
clock and agenda, weather and plug summary, and home controls and music. Buttons have a
minimum 48×48 logical-pixel touch target, with spacing between adjacent controls.
Use native display resolution and check OS display scaling on the target device.

The clock, configured weather locations, and Google Calendar retain their live
connections. Weather and agenda lists scroll independently with touch inertia;
mouse dragging also works for touch drivers that report mouse input. Calendar
entries include their start time or an all-day label.

Thermostat, lights, and door are clearly marked
as not connected. Thermostat buttons are disabled until
integrations are added. Climate and Security navigation opens a
placeholder page with a working return button. No device status is simulated.

## Spotify card

The existing Spotify connection supplies the current track, artists, and playback
state. Album artwork downloads asynchronously into an Avalonia bitmap, reuses the
current cover while its URL is unchanged, and falls back to a music icon if missing
or unavailable. Downloads are buffered before bitmap decoding on the UI thread;
failed covers are retried at most once per minute. A UI dispatcher timer drives
refreshes. Playback requests cannot overlap; artwork loads independently so a
slow cover download does not disable the transport controls. Stale downloads
cannot replace the current track's cover.

Previous, play/pause, and next controls keep 48-pixel minimum touch targets within
the existing card margins. Larger artwork fills the card behind a dark gradient,
with track details and controls overlaid inside the padding. Long track names
wrap to two lines and then truncate.
Controls are disabled during user playback commands and when no track is available.
Routine polling leaves buttons enabled; a command pressed during a poll waits for
that read to finish instead of being dropped. Open Spotify
on a playback device to start listening. Playback refreshes every five seconds;
connection failures show an offline state and retry automatically.

## Smart plug

The Plugs page replaces Lighting and shows a vertically scrollable list with independent power and refresh controls (56 pixels high). The homepage summary shows confirmed on/off counts and unknown states, with a View plugs shortcut. Each registered ISmartPlugService gets its own card and polling lifecycle. The current registration connects one plug; additional physical plugs need separate service connections or a bridge/service extension for discovery and per-device addressing. No multi-device API is assumed.

The existing plug connection uses a local HTTP bridge at `http://127.0.0.1:8765/`.
This is a bridge API, not a direct connection to the Tapo device. The bridge must
run on the same machine as the dashboard (on the Pi when deployed there).
The typed client is registered with `AddHttpClient`, supplied by the
`Microsoft.Extensions.Http` package, with a 10-second timeout.

Expected endpoints:

- `GET /api/plug`: JSON such as `{"name":"Desk lamp","isOn":false}`.
  The boolean `isOn` is required; missing or malformed state is treated as unavailable.
- `POST /api/plug/on` and `POST /api/plug/off`: return a successful HTTP status.
  The dashboard then fetches the state again to display the confirmed result.

State refreshes on startup and every 30 seconds. Each Refresh button refreshes its plug
manually. Power controls are disabled until a valid state has loaded and while
requests are running. Failed requests show an unavailable status; a successful
refresh restores control. Pending requests and polling stop when the app closes.

## Run locally

Install the .NET 10 SDK, configure the files described below, then run from the
project folder:

```sh
dotnet run --project DigitalDashboard.csproj
```

The app opens fullscreen and needs an internet connection for weather and calendar
updates. On a Raspberry Pi, run it in a graphical desktop session.

## Weather locations

Personal locations belong in `appsettings.json`, which Git ignores. The repository
includes `appsettings.example.json` with an empty list and no personal locations.

Copy the example to `appsettings.json` in the project folder and add your places:

```json
{
  "WeatherLocations": [
    {
      "Name": "Your town",
      "Latitude": 0.0,
      "Longitude": 0.0
    }
  ]
}
```

Replace the example name and coordinates with your own. Add more objects to the
list to display more places. Names are display labels; coordinates determine the
weather requested from Open-Meteo.

Your settings file is copied alongside the application when building or publishing.
For a deployed app, edit that copy and restart. If the file is missing, the app
creates an empty one beside the executable and keeps displaying the clock.

Weather refreshes every 10 minutes. Restart the app after changing its locations.

## Google Calendar

The dashboard reads your primary calendar using read-only access. It loads up to
10 events in the next 14 days, ordered by start time, and refreshes every five
minutes. Recurring events are expanded into individual occurrences. Timed events
use the device's local time zone.

### Credentials and first sign-in

1. Enable the Google Calendar API in your Google Cloud project and configure its
   OAuth consent screen. See Google's [Calendar API setup guide](https://developers.google.com/workspace/calendar/api/quickstart/python).
2. Create an OAuth client with the **Desktop app** application type and download
   its credentials JSON. If your external app is in testing, add your account as a
   test user.
3. Create the application data folder below and save the downloaded file there as
   `google-credentials.json`.
4. Launch DigitalDashboard and complete the browser sign-in to grant calendar access.
   For initial setup on a Raspberry Pi, use a browser in its local desktop session.

The service uses `Environment.SpecialFolder.ApplicationData` for the current user:

| Platform | Application data folder |
| --- | --- |
| Windows | `%APPDATA%\DigitalDashboard` |
| Linux / Raspberry Pi | Normally `~/.config/DigitalDashboard`; when configured, `$XDG_CONFIG_HOME/DigitalDashboard` |

Within that folder, the app expects this structure:

```text
DigitalDashboard/
  google-credentials.json
  google-tokens/            # Created automatically after authorization
```

The credentials identify the application. OAuth tokens represent the access you
granted to your calendar. Tokens are saved using Google's `FileDataStore` and reused
on subsequent launches. Run the app as the same OS user to reuse that user's tokens.
An earlier version used Google's default token location; switching to the dedicated
folder may require signing in again.

### Troubleshooting

If no events appear, check that the credentials are in the correct user's application
data folder, sign-in has completed, and the primary calendar has events within the
next 14 days. The UI currently shows the same empty message when the initial calendar
load fails; errors are written to debug output. A failed refresh preserves previously
loaded events.

## Private files

Keep credentials, OAuth tokens, and personal `appsettings.json` out of Git. The
repository ignores `google-credentials.json`, `client_secret*.json`, `credentials.json`,
`Google.Apis.Auth/`, `google-tokens/`, and `tokens/`, as well as personal settings.
Credentials and tokens belong in the application data folder, outside the checkout.
Ignore rules do not remove files that were already tracked by Git.

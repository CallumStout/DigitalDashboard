# DigitalDashboard

A fullscreen Avalonia dashboard with a live clock, weather cards, and upcoming
Google Calendar events. Built with .NET 10.

The dark home overview is designed for 7-inch landscape displays at 800×480 and
1024×600. A persistent left navigation rail sits beside three flexible columns:
clock and agenda, weather and scenes, and home controls and music. Buttons have a
minimum 48×48 logical-pixel touch target, with spacing between adjacent controls.
Use native display resolution and check OS display scaling on the target device.

The clock, configured weather locations, and Google Calendar retain their live
connections. Weather and agenda lists scroll independently with touch inertia;
mouse dragging also works for touch drivers that report mouse input. Calendar
entries include their start time or an all-day label.

Thermostat, lights, door, Spotify, scenes, and forecast slots are clearly marked
as not connected. Playback, thermostat, and scene buttons are disabled until
integrations are added. Climate, Lighting, and Security navigation opens a
placeholder page with a working return button. No device status is simulated.

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

# DigitalClock

A simple Avalonia clock and weather dashboard.

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

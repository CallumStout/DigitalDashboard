using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using DigitalDashboard.ViewModels;
using DigitalDashboard.Views;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using DigitalDashboard.Services;
using System;
using System.Net.Http;
using DigitalDashboard.Models;
using System.IO;
using System.Text.Json;

namespace DigitalDashboard
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }


        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is
                IClassicDesktopStyleApplicationLifetime desktop)
            {
                DisableAvaloniaDataAnnotationValidation();

                var services = new ServiceCollection();
                var settings = LoadSettings();

                services.AddSingleton(settings);

                // Register our services
                services.AddSingleton<IClockService, ClockService>();
                services.AddSingleton(_ => new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(10)
                });
                services.AddSingleton<IWeatherService, WeatherService>();

                // Register our ViewModel
                services.AddTransient<ClockViewModel>();

                var serviceProvider = services.BuildServiceProvider();

                desktop.MainWindow = new MainWindow
                {
                    DataContext =
                        serviceProvider.GetRequiredService<ClockViewModel>()
                };

                desktop.Exit += (_, _) =>
                {
                    (desktop.MainWindow.DataContext as IDisposable)?.Dispose();
                    serviceProvider.Dispose();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }


        private static AppSettings LoadSettings()
        {
            var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

            if (!File.Exists(settingsPath))
            {
                var emptySettings = new AppSettings
                {
                    WeatherLocations = new()
                };

                File.WriteAllText(settingsPath, JsonSerializer.Serialize(emptySettings,
                    new JsonSerializerOptions { WriteIndented = true }));
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath));

            if (settings?.WeatherLocations == null)
            {
                throw new InvalidDataException("appsettings.json must contain a WeatherLocations list.");
            }

            foreach (var location in settings.WeatherLocations)
            {
                if (location == null || string.IsNullOrWhiteSpace(location.Name)
                    || !double.IsFinite(location.Latitude) || location.Latitude < -90 || location.Latitude > 90
                    || !double.IsFinite(location.Longitude) || location.Longitude < -180 || location.Longitude > 180)
                {
                    throw new InvalidDataException("Each weather location needs a name and valid coordinates in appsettings.json.");
                }
            }

            return settings;
        }


        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}

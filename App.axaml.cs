using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using DigitalClock.ViewModels;
using DigitalClock.Views;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using DigitalClock.Services;

namespace DigitalClock
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

                // Register our services
                services.AddSingleton<IClockService, ClockService>();

                // Register our ViewModel
                services.AddTransient<ClockViewModel>();

                var serviceProvider = services.BuildServiceProvider();

                desktop.MainWindow = new MainWindow
                {
                    DataContext =
                        serviceProvider.GetRequiredService<ClockViewModel>()
                };
            }

            base.OnFrameworkInitializationCompleted();
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
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.IO;
using System.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Recode.Core.Services.Compression;
using Recode.Core.Services.Ffmpeg;
using Recode.Core.Services.History;
using Recode.Core.Services.Power;
using Recode.Core.Services.Settings;
using Recode.Core.Utility;
using Recode.Infrastructure.Services.Compression;
using Recode.Infrastructure.Services.FfMpeg;
using Recode.Infrastructure.Services.History;
using Recode.Infrastructure.Services.Power;
using Recode.Infrastructure.Services.Settings;
using Recode.ViewModels;
using Recode.Views;

namespace Recode;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            // Create the registration list
            ServiceCollection services = new();

            // Register ViewModels
            services.AddSingleton<MainWindowViewModel>();

            // Register services
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IFfmpegManager, FfmpegManager>();
            services.AddSingleton<IFfMpegService, FfMpegService>();
            services.AddSingleton<ICompressionService, CompressionService>();
            services.AddSingleton<IPowerService, PowerService>();
            services.AddSingleton<IHistoryService, HistoryService>();

            // Build the factory
            ServiceProvider provider = services.BuildServiceProvider();

            // Let DI create the ViewModel with all its dependencies resolved
            var viewModel = provider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };

            // Handle command line arguments (files dragged onto exe or "Open with")
            if (desktop.Args is { Length: > 0 })
            {
                var videoFiles = desktop.Args
                    .Where(File.Exists)
                    .Where(path => VideoFiles.Extensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
                    .ToList();

                if (videoFiles.Count > 0)
                {
                    // Add files after window is shown to ensure UI is ready for dialogs
                    desktop.MainWindow.Opened += async (_, _) =>
                    {
                        await viewModel.AddFilesWithHistoryCheckAsync(videoFiles);
                    };
                }
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        DataAnnotationsValidationPlugin[] dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (DataAnnotationsValidationPlugin plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
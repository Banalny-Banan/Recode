using Avalonia.Controls;
using Avalonia.Interactivity;
using Recode.ViewModels;

namespace Recode.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not MainWindowViewModel vm) return;

            (bool success, string? message, string? path) = await vm.InitializeFfmpeg();

            if (!success)
            {
                await AppDialog.ShowError("FFmpeg Required",
                    $"Failed to download FFmpeg:\n{message}\n\nTry again or download and place ffmpeg.exe in {path}.");
                Close();
            }
        }
        catch
        {
            Close();
        }
    }
}

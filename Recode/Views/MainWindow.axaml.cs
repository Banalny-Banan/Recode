using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Recode.ViewModels;

namespace Recode.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CountdownVisible)
            && sender is MainWindowViewModel { CountdownVisible: true })
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            Activate();
            Topmost = true;
            Topmost = false;
        }
    }

    async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not MainWindowViewModel vm) return;

            var (success, message, path) = await vm.InitializeFfmpeg();

            if (!success)
            {
                Window dialog = new()
                {
                    Title = "FFmpeg Required",
                    Width = 400, Height = 150,
                    CanResize = false,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = new TextBlock
                    {
                        Text = $"Failed to download FFmpeg:\n{message}\n\nTry again or download and place ffmpeg.exe in {path}.",
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(16),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    },
                };
                await dialog.ShowDialog(this);
                Close();
            }
        }
        catch
        {
            Close();
        }
    }
}
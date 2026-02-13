using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Recode.ViewModels;

namespace Recode.Views;

public partial class CompressionSettings : UserControl
{
    static readonly string DefaultOutputPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Recode");

    public CompressionSettings()
    {
        InitializeComponent();
    }

    async void OutputPath_OnTapped(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var createdDefault = false;

            if (!Directory.Exists(DefaultOutputPath))
            {
                Directory.CreateDirectory(DefaultOutputPath);
                createdDefault = true;
            }

            IStorageFolder? startFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(DefaultOutputPath);

            IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = "Select output folder",
                SuggestedStartLocation = startFolder,
            });

            if (folders.Count > 0 && DataContext is MainWindowViewModel vm)
            {
                vm.OutputPath = folders[0].Path.LocalPath;
            }

            // Clean up if we created the folder and it wasn't selected (or dialog was cancelled)
            if (createdDefault
                && Directory.Exists(DefaultOutputPath)
                && Directory.GetFileSystemEntries(DefaultOutputPath).Length == 0
                && (folders.Count == 0 || folders[0].Path.LocalPath != DefaultOutputPath))
            {
                Directory.Delete(DefaultOutputPath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error picking output folder: {ex.Message}");
        }
    }
}
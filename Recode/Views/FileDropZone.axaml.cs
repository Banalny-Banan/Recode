using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Recode.ViewModels;

namespace Recode.Views;

public partial class FileDropZone : UserControl
{
    static readonly HashSet<string> VideoExtensions =
    [
        ".mp4", ".mkv", ".avi",
        ".mov", ".flv", ".wmv",
        ".webm", ".ts", ".m2ts",
    ];

    static readonly FilePickerFileType VideoFiles = new("Video files")
    {
        Patterns = VideoExtensions.Select(ext => $"*{ext}").ToList(),
    };

    public FileDropZone()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    async void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File))
            return;

        if (e.DataTransfer.TryGetFiles() is not IEnumerable<IStorageItem> files)
            return;

        List<string> paths = files
            .Select(f => f.Path.LocalPath)
            .Where(p => VideoExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()))
            .ToList();

        if (DataContext is MainWindowViewModel vm)
            await AddFilesWithHistoryCheck(vm, paths);
    }

    async void BrowseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);

            if (topLevel == null)
                return;

            IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select video files",
                AllowMultiple = true,
                FileTypeFilter = [VideoFiles],
            });

            if (files.Count > 0 && DataContext is MainWindowViewModel vm)
            {
                List<string> paths = files.Select(f => f.Path.LocalPath).ToList();
                await AddFilesWithHistoryCheck(vm, paths);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error picking files: {ex.Message}");
        }
    }

    async Task AddFilesWithHistoryCheck(MainWindowViewModel vm, List<string> paths)
    {
        List<string> alreadyCompressed = paths.Where(vm.IsAlreadyCompressed).ToList();

        if (alreadyCompressed.Count > 0)
        {
            bool addAll = await ShowAlreadyProcessedDialog(alreadyCompressed.Count, paths.Count);

            if (!addAll)
                paths = paths.Except(alreadyCompressed).ToList();
        }

        vm.AddFiles(paths);
    }

    async Task<bool> ShowAlreadyProcessedDialog(int processedCount, int totalCount)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return true;

        var result = false;

        string message = processedCount == totalCount
            ? processedCount == 1
                ? "This file has already been compressed. Add it anyway?"
                : $"All {processedCount} files have already been compressed. Add them anyway?"
            : $"{processedCount} of {totalCount} files have already been compressed. Add them anyway?";

        Window dialog = new()
        {
            Title = "Already Compressed",
            Width = 400, Height = 150,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(16),
                Spacing = 16,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 8,
                        Children =
                        {
                            new Button
                            {
                                Content = "Yes",
                                MinWidth = 80,
                            },
                            new Button
                            {
                                Content = "No",
                                MinWidth = 80,
                            },
                        },
                    },
                },
            },
        };

        var buttonPanel = (StackPanel)((StackPanel)dialog.Content).Children[1];
        var yesButton = (Button)buttonPanel.Children[0];
        var noButton = (Button)buttonPanel.Children[1];

        yesButton.Click += (_, _) =>
        {
            result = true;
            dialog.Close();
        };

        noButton.Click += (_, _) =>
        {
            result = false;
            dialog.Close();
        };

        await dialog.ShowDialog(owner);
        return result;
    }
}
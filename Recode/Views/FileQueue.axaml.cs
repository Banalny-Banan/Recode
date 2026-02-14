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
using Recode.Core.Utility;
using Recode.ViewModels;

namespace Recode.Views;

public partial class FileQueue : UserControl
{
    static readonly FilePickerFileType VideoFileType = new("Video files")
    {
        Patterns = VideoFiles.Extensions.Select(ext => $"*{ext}").ToList(),
    };

    public FileQueue()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);

        if (Design.IsDesignMode)
        {
            DataContext = new MainWindowViewModel
            {
                QueueItems =
                {
                    new("video1.mp4", "1.2 GB", 60, QueueItemStatus.Processing),
                    new("video2_very_long_video_name_that_will_make_you_bored.mkv", "800 MB", 0, QueueItemStatus.Pending),
                    new("video3.avi", "999 MB → 99.9 MB", 100, QueueItemStatus.Completed),
                    new("video4.mov", "3 GB", 25, QueueItemStatus.Failed),
                    new("video5.flv", "500 MB", 90, QueueItemStatus.Pending),
                    new("video6.wmv", "1 GB", 10, QueueItemStatus.Pending),
                    new("video7.mp4", "700 MB", 75, QueueItemStatus.Pending),
                    new("video8.mkv", "1.5 GB", 50, QueueItemStatus.Pending),
                },
            };
        }
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
            .Where(p => VideoFiles.Extensions.Contains(Path.GetExtension(p).ToLowerInvariant()))
            .ToList();

        if (DataContext is MainWindowViewModel vm)
            await vm.AddFilesWithHistoryCheckAsync(paths);
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
                FileTypeFilter = [VideoFileType],
            });

            if (files.Count > 0 && DataContext is MainWindowViewModel vm)
            {
                List<string> paths = files.Select(f => f.Path.LocalPath).ToList();
                await vm.AddFilesWithHistoryCheckAsync(paths);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error picking files: {ex.Message}");
        }
    }
}
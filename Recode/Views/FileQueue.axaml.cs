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

public partial class FileQueue : UserControl
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
                    new("video1.mp4", "1.2 GB", 60, "ETA 2:30"),
                    new("video2.mkv", "800 MB", 0, "Pending"),
                    new("video3.avi", "999 MB → 99.9 MB", 100, "Completed"),
                    new("video4.mov", "3 GB", 25, "ETA 5:00"),
                    new("video5.flv", "500 MB", 90, "ETA 0:30"),
                    new("video6.wmv", "1 GB", 10, "ETA 10:00"),
                    new("video7.mp4", "700 MB", 75, "ETA 1:00"),
                    new("video8.mkv", "1.5 GB", 50, "ETA 3:00"),
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
            string message = alreadyCompressed.Count == paths.Count
                ? alreadyCompressed.Count == 1
                    ? "This file has already been compressed. Add it anyway?"
                    : $"All {alreadyCompressed.Count} files have already been compressed. Add them anyway?"
                : $"{alreadyCompressed.Count} of {paths.Count} files have already been compressed. Add them anyway?";

            bool addAll = await AppDialog.AskYesNo("Already Compressed", message);

            if (!addAll)
                paths = paths.Except(alreadyCompressed).ToList();
        }

        vm.AddFiles(paths);
    }
}
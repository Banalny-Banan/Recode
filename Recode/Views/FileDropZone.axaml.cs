using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

    void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File))
            return;

        if (e.DataTransfer.TryGetFiles() is not IEnumerable<IStorageItem> files)
            return;

        IEnumerable<string> paths = files
            .Select(f => f.Path.LocalPath)
            .Where(p => VideoExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()));

        if (DataContext is MainWindowViewModel vm)
            vm.AddFiles(paths);
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
                vm.AddFiles(files.Select(f => f.Path.LocalPath));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error picking files: {ex.Message}");
        }
    }
}
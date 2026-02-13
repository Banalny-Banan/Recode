using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recode.Core.Utility;

namespace Recode.ViewModels;

public partial class QueueItemViewModel : ViewModelBase
{
    readonly Action<QueueItemViewModel>? _removeAction;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(SizeDisplay))]
    string _fileSize;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanRetry))]
    double _progress;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanRetry), nameof(ProgressBarBrush))]
    QueueItemStatus _status = QueueItemStatus.Pending;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(SizeDisplay))]
    string? _resultSize;

    public QueueItemViewModel(string filePath, Action<QueueItemViewModel> removeAction)
    {
        FileInfo info = new(filePath);
        FilePath = filePath;
        FileName = info.Name;
        _fileSize = Formatting.FormatFileSize(info.Length);
        _removeAction = removeAction;
    }

    internal QueueItemViewModel(string fileName, string fileSize, double progress, QueueItemStatus status)
    {
        FilePath = "";
        FileName = fileName;
        _fileSize = fileSize;
        Progress = progress;
        Status = status;
    }

    public string FilePath { get; }
    public string FileName { get; }

    public string SizeDisplay => ResultSize is null ? FileSize : $"{FileSize} → {ResultSize}";

    public bool CanRetry => Status is QueueItemStatus.Completed or QueueItemStatus.Failed;

    public IBrush ProgressBarBrush
    {
        get
        {
            if (Status == QueueItemStatus.Failed)
            {
                return Application.Current?.TryFindResource("SystemFillColorCriticalBrush", out object? resource) == true
                    ? resource as IBrush ?? Brushes.Crimson
                    : Brushes.Crimson;
            }

            return Application.Current?.TryFindResource("SystemAccentColor", out object? accent) == true
                ? accent as IBrush ?? Brushes.DodgerBlue
                : Brushes.DodgerBlue;
        }
    }

    [RelayCommand]
    void Retry()
    {
        Progress = 0;
        ResultSize = null;
        Status = QueueItemStatus.Pending;

        if (!string.IsNullOrEmpty(FilePath))
            FileSize = Formatting.FormatFileSize(new FileInfo(FilePath).Length);
    }

    [RelayCommand]
    void Remove() => _removeAction?.Invoke(this);
}

public enum QueueItemStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
}
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Recode.Core.Utility;

namespace Recode.ViewModels;

public partial class QueueItemViewModel : ViewModelBase
{
    readonly string _fileSize;

    [ObservableProperty]
    double _progress;

    [ObservableProperty]
    string _status = QueueItemStatus.Pending;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(SizeDisplay))]
    string? _resultSize;

    public QueueItemViewModel(string filePath)
    {
        FileInfo info = new(filePath);
        FilePath = filePath;
        FileName = info.Name;
        _fileSize = Formatting.FormatFileSize(info.Length);
    }

    internal QueueItemViewModel(string fileName, string fileSize, double progress, string status)
    {
        FilePath = "";
        FileName = fileName;
        _fileSize = fileSize;
        Progress = progress;
        Status = status;
    }

    public string FilePath { get; }
    public string FileName { get; }

    public string SizeDisplay => ResultSize is null ? _fileSize : $"{_fileSize} → {ResultSize}";
}

public static class QueueItemStatus
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
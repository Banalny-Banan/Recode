using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Recode.Core;

namespace Recode.ViewModels;

public partial class QueueItemViewModel : ViewModelBase
{
    [ObservableProperty]
    double _progress;

    [ObservableProperty]
    string _status = QueueItemStatus.Pending;

    public QueueItemViewModel(string filePath)
    {
        FileInfo info = new(filePath);
        FilePath = filePath;
        FileName = info.Name;
        FileSize = Formatting.FormatFileSize(info.Length);
    }

    internal QueueItemViewModel(string fileName, string fileSize, double progress, string status)
    {
        FilePath = "";
        FileName = fileName;
        FileSize = fileSize;
        Progress = progress;
        Status = status;
    }

    public string FilePath { get; }
    public string FileName { get; }
    public string FileSize { get; }
}

public static class QueueItemStatus
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
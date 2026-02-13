using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recode.Core;
using Recode.Core.Services.FfmpegManager;
using Recode.Core.Services.Settings;

namespace Recode.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    readonly IFfmpegManager? _ffmpegManager;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(StartButtonEnabled))]
    bool _ffMpegReady;

    [ObservableProperty]
    Codec _selectedCodec;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(QualityLabel))]
    int _qualityValue;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(OutputPathTooltip))]
    string _outputPath;

    [ObservableProperty]
    bool _replaceFiles;

    [ObservableProperty]
    AfterCompletionAction _afterCompletionAction;

    [ObservableProperty]
    bool _cancelButtonEnabled;

    [ObservableProperty]
    double _ffMpegDownloadProgress;

    public MainWindowViewModel(IFfmpegManager ffmpegManager, ISettingsService settingsService)
    {
        _ffmpegManager = ffmpegManager;
        _settingsService = settingsService;
        AppSettings settings = _settingsService.Load();
        _selectedCodec = settings.SelectedCodec;
        _qualityValue = settings.QualityValue;
        _outputPath = settings.OutputPath;
        _replaceFiles = settings.ReplaceFiles;
        _afterCompletionAction = settings.AfterCompletionAction;
    }

    public MainWindowViewModel()
    {
        _selectedCodec = Codec.H264;
        _qualityValue = 50;
        _outputPath = "";
        _replaceFiles = false;
        _afterCompletionAction = AfterCompletionAction.Nothing;
    }

    public string QualityLabel => QualityValue switch
    {
        <= 15 => "Smallest",
        <= 35 => "Smaller",
        <= 65 => "Balanced",
        <= 85 => "High Quality",
        _ => "Lossless",
    };

    public bool StartButtonEnabled => FfMpegReady;

    public string OutputPathTooltip => string.IsNullOrEmpty(OutputPath) ? "Select output folder" : OutputPath;

    public double OverallProgress => QueueItems.Count == 0 ? 0 : QueueItems.Average(item => item.Progress);

    public ObservableCollection<QueueItemViewModel> QueueItems { get; } = [];

    public void AddFiles(IEnumerable<string> filePaths)
    {
        foreach (string path in filePaths)
        {
            // skip duplicates
            if (QueueItems.Any(item => item.FilePath == path))
                continue;

            QueueItems.Add(new QueueItemViewModel(path));
        }
    }

    [RelayCommand]
    async Task StartCompression()
    {
        // start compression of all items in queue
    }

    [RelayCommand]
    async Task CancelCompression()
    {
        // clear all items from queue
    }

    public async Task<(bool Success, string? Message)> InitializeFfmpeg()
    {
        if (_ffmpegManager is null)
        {
            FfMpegReady = true;
            return (true, null);
        }

        Progress<double> progressReporter = new(p => FfMpegDownloadProgress = p);
        (bool success, string? message) = await _ffmpegManager.EnsureAvailableAsync(progressReporter);

        if (success)
        {
            FfMpegReady = true;
            return (true, null);
        }

        FfMpegReady = false;
        return (false, message);
    }
}

public static class QueueItemStatus
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
}
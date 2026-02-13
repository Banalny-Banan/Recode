using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recode.Core.Enums;
using Recode.Core.Services.Compression;
using Recode.Core.Services.FfmpegManager;
using Recode.Core.Services.FfMpegService;
using Recode.Core.Services.Settings;
using Recode.Core.Utility;

namespace Recode.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    readonly IFfmpegManager? _ffmpegManager;
    readonly ICompressionService? _compressionService;
    CancellationTokenSource? _compressionCts;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(StartButtonEnabled))]
    bool _ffMpegReady;

    [ObservableProperty]
    bool _isCompressing;

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

    public MainWindowViewModel(IFfmpegManager ffmpegManager, ICompressionService compressionService, ISettingsService settingsService)
    {
        _ffmpegManager = ffmpegManager;
        _compressionService = compressionService;
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
        if (_compressionService is null || IsCompressing)
            return;

        IsCompressing = true;
        CancelButtonEnabled = true;
        _compressionCts = new CancellationTokenSource();
        CompressionOptions options = new(SelectedCodec, QualityValue);
        OutputOptions output = new(OutputPath, ReplaceFiles);

        try
        {
            while (NextPendingItem() is { } item)
            {
                item.Status = QueueItemStatus.Processing;

                Progress<double> progress = new(p =>
                {
                    item.Progress = p;
                    OnPropertyChanged(nameof(OverallProgress));
                });

                CompressionResult result = await _compressionService.CompressFileAsync(
                    item.FilePath, options, output, progress, _compressionCts.Token);

                if (result.Success)
                {
                    item.Progress = 100;
                    item.ResultSize = Formatting.FormatFileSize(result.OutputSize);
                    item.Status = QueueItemStatus.Completed;
                }
                else
                {
                    item.Progress = 0;

                    if (_compressionCts.IsCancellationRequested)
                    {
                        item.Status = QueueItemStatus.Pending;
                    }
                    else
                    {
                        item.Status = QueueItemStatus.Failed;
                    }
                }

                OnPropertyChanged(nameof(OverallProgress));

                if (_compressionCts.IsCancellationRequested)
                    break;
            }
        }
        finally
        {
            IsCompressing = false;
            CancelButtonEnabled = false;
            _compressionCts?.Dispose();
            _compressionCts = null;
        }
    }

    [RelayCommand]
    void CancelCompression()
        => _compressionCts?.Cancel();

    QueueItemViewModel? NextPendingItem()
        => QueueItems.FirstOrDefault(item => item.Status == QueueItemStatus.Pending);

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
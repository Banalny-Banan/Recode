using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Recode.Core.Enums;
using Recode.Core.Services.Compression;
using Recode.Core.Services.FfmpegManager;
using Recode.Core.Services.History;
using Recode.Core.Services.Power;
using Recode.Core.Services.Settings;

namespace Recode.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    readonly IHistoryService? _historyService;

    [ObservableProperty]
    Codec _selectedCodec;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(QualityPercentage))]
    int _qualityValue;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(OutputPathTooltip)), NotifyPropertyChangedFor(nameof(StartButtonEnabled))]
    string _outputPath;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(StartButtonEnabled))]
    bool _replaceFiles;

    [ObservableProperty]
    bool _useGpu;

    [ObservableProperty]
    AfterCompletionAction _afterCompletionAction;

    public MainWindowViewModel(IFfmpegManager ffmpegManager, ICompressionService compressionService, IPowerService powerService, IHistoryService historyService, ISettingsService settingsService)
    {
        _ffmpegManager = ffmpegManager;
        _compressionService = compressionService;
        _powerService = powerService;
        _historyService = historyService;
        _settingsService = settingsService;
        AppSettings settings = _settingsService.Load();
        _selectedCodec = settings.SelectedCodec;
        _qualityValue = settings.QualityValue;
        _outputPath = settings.OutputPath;
        _replaceFiles = settings.ReplaceFiles;
        _useGpu = settings.UseGpu;
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
    
    public string QualityPercentage => $"{QualityValue}%";

    public bool StartButtonEnabled => FfMpegReady && (!string.IsNullOrEmpty(OutputPath) || ReplaceFiles);

    public string OutputPathTooltip => string.IsNullOrEmpty(OutputPath) ? "Select output folder" : OutputPath;

    public double OverallProgress => QueueItems.Count == 0 ? 0 : QueueItems.Average(item => item.Progress);

    public ObservableCollection<QueueItemViewModel> QueueItems { get; } = [];

    public bool IsAlreadyCompressed(string filePath)
        => _historyService?.IsCompressed(filePath) ?? false;

    public void AddFiles(IEnumerable<string> filePaths)
    {
        foreach (string path in filePaths)
        {
            if (QueueItems.Any(item => item.FilePath == path))
                continue;

            QueueItems.Add(new QueueItemViewModel(path, RemoveItem, NotifyProgressChanged));
        }
    }

    void RemoveItem(QueueItemViewModel item)
    {
        if (item.Status == QueueItemStatus.Processing)
            _currentItemCts?.Cancel();

        QueueItems.Remove(item);
        OnPropertyChanged(nameof(OverallProgress));
    }

    void NotifyProgressChanged() => OnPropertyChanged(nameof(OverallProgress));
}
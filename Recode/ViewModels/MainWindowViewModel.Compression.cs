using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recode.Core.Services.Compression;
using Recode.Core.Services.Ffmpeg;
using Recode.Core.Utility;

namespace Recode.ViewModels;

public partial class MainWindowViewModel
{
    readonly ICompressionService? _compressionService;
    CancellationTokenSource? _compressionCts;
    CancellationTokenSource? _currentItemCts;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(CancelButtonEnabled))]
    bool _isCompressing;

    public bool CancelButtonEnabled => IsCompressing;

    [RelayCommand]
    async Task StartCompression()
    {
        if (_compressionService is null || IsCompressing)
            return;

        IsCompressing = true;
        _compressionCts = new CancellationTokenSource();
        FfMpegOptions options = new(SelectedCodec, QualityValue, UseGpu);
        OutputOptions output = new(OutputPath, ReplaceFiles);

        try
        {
            while (NextPendingItem() is { } item)
            {
                item.Status = QueueItemStatus.Processing;
                _currentItemCts = CancellationTokenSource.CreateLinkedTokenSource(_compressionCts.Token);

                Progress<double> progress = new(p =>
                {
                    item.Progress = p;
                    OnPropertyChanged(nameof(OverallProgress));
                });

                CompressionResult result = await _compressionService.CompressFileAsync(
                    item.FilePath, options, output, progress, _currentItemCts.Token);

                _currentItemCts.Dispose();
                _currentItemCts = null;

                // Item was removed during processing — skip to next
                if (!QueueItems.Contains(item))
                    continue;

                if (result.Success)
                {
                    item.Progress = 100;
                    item.ResultSize = Formatting.FormatFileSize(result.OutputSize);
                    item.Status = QueueItemStatus.Completed;
                    _historyService?.RecordCompressed(result.OutputPath);
                }
                else if (_compressionCts.IsCancellationRequested)
                {
                    item.Progress = 0;
                    item.Status = QueueItemStatus.Pending;
                }
                else
                {
                    item.Progress = Math.Max(item.Progress, 5);
                    item.Status = QueueItemStatus.Failed;
                    Debug.WriteLine($"Compression failed for {item.FilePath}: {result.ErrorMessage}");
                }

                OnPropertyChanged(nameof(OverallProgress));

                if (_compressionCts.IsCancellationRequested)
                    break;
            }
        }
        finally
        {
            bool wasCancelled = _compressionCts?.IsCancellationRequested ?? false;
            IsCompressing = false;
            _compressionCts?.Dispose();
            _compressionCts = null;

            if (!wasCancelled)
                await ExecuteAfterCompletionAction();
        }
    }

    QueueItemViewModel? NextPendingItem()
        => QueueItems.FirstOrDefault(item => item.Status == QueueItemStatus.Pending);

    [RelayCommand]
    void CancelCompression() => _compressionCts?.Cancel();
}
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

    [ObservableProperty]
    bool _isCompressing;

    [ObservableProperty]
    bool _cancelButtonEnabled;

    [RelayCommand]
    async Task StartCompression()
    {
        if (_compressionService is null || IsCompressing)
            return;

        IsCompressing = true;
        CancelButtonEnabled = true;
        _compressionCts = new CancellationTokenSource();
        FfMpegOptions options = new(SelectedCodec, QualityValue);
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
                    _historyService?.RecordCompressed(result.OutputPath);
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
                        Debug.WriteLine($"Compression failed for {item.FilePath}: {result.ErrorMessage}");
                    }
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
            CancelButtonEnabled = false;
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
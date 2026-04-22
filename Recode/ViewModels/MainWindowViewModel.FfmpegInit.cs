using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Recode.Core.Services.Ffmpeg;

namespace Recode.ViewModels;

public partial class MainWindowViewModel
{
    readonly IFfmpegManager? _ffmpegManager;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(StartButtonEnabled))]
    bool _ffMpegReady;

    [ObservableProperty]
    double _ffMpegDownloadProgress;

    public async Task<(bool Success, string? Message, string? FfMpegRequiredPath)> InitializeFfmpeg()
    {
        if (_ffmpegManager is null)
        {
            FfMpegReady = true;
            return (true, null, null);
        }

        Progress<double> progressReporter = new(p => FfMpegDownloadProgress = p);
        (bool success, string? message) = await _ffmpegManager.EnsureAvailableAsync(progressReporter);

        if (success)
        {
            FfMpegReady = true;
            return (true, null, null);
        }

        FfMpegReady = false;
        return (false, message, _ffmpegManager.FfmpegPath);
    }
}
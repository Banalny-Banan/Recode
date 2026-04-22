namespace Recode.Core.Services.Ffmpeg;

public interface IFfmpegManager
{
    string FfmpegPath { get; }

    Task<(bool Success, string? Message)> EnsureAvailableAsync(IProgress<double> progress, CancellationToken cancellationToken = default);
}
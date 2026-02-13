namespace Recode.Core.Services.FfmpegManager;

public interface IFfmpegManager
{
    string FfmpegPath { get; }

    Task<(bool Success, string? Message)> EnsureAvailableAsync(IProgress<double> progress);
}
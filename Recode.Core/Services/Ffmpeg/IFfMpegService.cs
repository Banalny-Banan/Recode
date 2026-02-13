using Recode.Core.Enums;

namespace Recode.Core.Services.Ffmpeg;

public interface IFfMpegService
{
    Task<FfMpegResult> CompressAsync
    (
        string inputPath,
        string outputPath,
        FfMpegOptions options,
        IProgress<double> progress,
        CancellationToken cancellationToken);
}

public record struct FfMpegResult(bool Success, string? ErrorMessage);
public record struct FfMpegOptions(Codec Codec, int Quality);
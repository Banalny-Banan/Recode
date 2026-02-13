using Recode.Core.Enums;

namespace Recode.Core.Services.Ffmpeg;

public interface IFfMpegService
{
    Task<CompressionResult> CompressAsync
    (
        string inputPath,
        string outputPath,
        CompressionOptions options,
        IProgress<double> progress,
        CancellationToken cancellationToken);
}

public record struct CompressionResult(bool Success, string? ErrorMessage, long OutputSize = 0);
public record struct CompressionOptions(Codec Codec, int Quality);
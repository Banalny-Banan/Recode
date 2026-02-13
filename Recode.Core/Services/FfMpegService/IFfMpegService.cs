using Recode.Core.Enums;

namespace Recode.Core.Services.FfMpegService;

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

public record struct CompressionResult(bool Success, string? ErrorMessage);
public record struct CompressionOptions(Codec Codec, int Quality);
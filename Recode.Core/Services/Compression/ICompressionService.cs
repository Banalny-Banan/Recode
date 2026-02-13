using Recode.Core.Services.Ffmpeg;

namespace Recode.Core.Services.Compression;

public interface ICompressionService
{
    Task<CompressionResult> CompressFileAsync
    (
        string inputPath,
        FfMpegOptions options,
        OutputOptions output,
        IProgress<double> progress,
        CancellationToken cancellationToken
    );
}

public record struct CompressionResult(bool Success, string? ErrorMessage, long OutputSize, string OutputPath);
public record struct OutputOptions(string OutputFolder, bool ReplaceOriginal);
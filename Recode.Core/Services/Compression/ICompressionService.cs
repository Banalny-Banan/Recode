using Recode.Core.Services.Ffmpeg;

namespace Recode.Core.Services.Compression;

public interface ICompressionService
{
    Task<CompressionResult> CompressFileAsync
    (
        string inputPath,
        CompressionOptions options,
        OutputOptions output,
        IProgress<double> progress,
        CancellationToken cancellationToken
    );
}

public record struct OutputOptions(string OutputFolder, bool ReplaceOriginal);
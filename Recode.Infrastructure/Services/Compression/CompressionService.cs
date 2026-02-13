using Recode.Core.Services.Compression;
using Recode.Core.Services.FfMpegService;

namespace Recode.Infrastructure.Services.Compression;

public class CompressionService(IFfMpegService ffMpegService) : ICompressionService
{
    public async Task<CompressionResult> CompressFileAsync
    (
        string inputPath,
        CompressionOptions options,
        OutputOptions output,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        string outputPath = ResolveOutputPath(inputPath, output);
        CompressionResult result = await ffMpegService.CompressAsync(inputPath, outputPath, options, progress, cancellationToken);

        if (result.Success && output.ReplaceOriginal)
            File.Move(outputPath, inputPath, true);

        return result;
    }

    static string ResolveOutputPath(string inputPath, OutputOptions output)
    {
        if (output.ReplaceOriginal)
            return $"{inputPath}.temp";

        Directory.CreateDirectory(output.OutputFolder);
        return Path.Combine(output.OutputFolder, Path.GetFileName(inputPath));
    }
}
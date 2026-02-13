using Recode.Core.Services.Compression;
using Recode.Core.Services.Ffmpeg;

namespace Recode.Infrastructure.Services.Compression;

public class CompressionService(IFfMpegService ffMpegService) : ICompressionService
{
    public async Task<CompressionResult> CompressFileAsync
    (
        string inputPath,
        FfMpegOptions options,
        OutputOptions output,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        string finalPath = ResolveOutputPath(inputPath, output);
        bool needsMove = string.Equals(Path.GetFullPath(finalPath), Path.GetFullPath(inputPath), StringComparison.OrdinalIgnoreCase);
        string outputPath = needsMove ? TempPathFor(inputPath) : finalPath;

        FfMpegResult result = await ffMpegService.CompressAsync(inputPath, outputPath, options, progress, cancellationToken);

        if (!result.Success)
            return new CompressionResult(false, result.ErrorMessage, 0, finalPath);

        long outputSize = new FileInfo(outputPath).Length;

        if (needsMove)
            File.Move(outputPath, finalPath, true);

        return new CompressionResult(true, null, outputSize, finalPath);
    }

    static string ResolveOutputPath(string inputPath, OutputOptions output)
    {
        if (output.ReplaceOriginal)
            return inputPath;

        Directory.CreateDirectory(output.OutputFolder);
        return Path.Combine(output.OutputFolder, Path.GetFileName(inputPath));
    }

    static string TempPathFor(string filePath)
    {
        string dir = Path.GetDirectoryName(filePath)!;
        string name = Path.GetFileNameWithoutExtension(filePath);
        string ext = Path.GetExtension(filePath);
        return Path.Combine(dir, $"{name}.temp{ext}");
    }
}
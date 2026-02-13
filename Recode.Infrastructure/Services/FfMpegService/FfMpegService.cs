using System.Text.RegularExpressions;
using CliWrap;
using Recode.Core.Enums;
using Recode.Core.Services.FfmpegManager;
using Recode.Core.Services.FfMpegService;

namespace Recode.Infrastructure.Services.FfMpegService;

public partial class FfMpegService(IFfmpegManager ffmpegManager) : IFfMpegService
{
    readonly string _ffmpegPath = ffmpegManager.FfmpegPath;

    public async Task<CompressionResult> CompressAsync
    (
        string inputPath,
        string outputPath,
        CompressionOptions options,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        try
        {
            TimeSpan duration = await ProbeDurationAsync(inputPath, cancellationToken);

            if (duration <= TimeSpan.Zero)
                return new CompressionResult(false, "Could not determine file duration");

            var lastStderrLine = "";

            CommandResult result = await Cli.Wrap(_ffmpegPath)
                .WithArguments(BuildArguments(inputPath, outputPath, options))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
                {
                    lastStderrLine = line;
                    TimeSpan? currentTime = ParseTime(line);

                    if (currentTime.HasValue)
                        progress.Report(currentTime.Value / duration * 100);
                }))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync(cancellationToken);

            if (result.ExitCode != 0)
                return new CompressionResult(false, lastStderrLine);

            return new CompressionResult(true, null);
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);

            return new CompressionResult(false, "Compression was cancelled");
        }
        catch (Exception ex)
        {
            return new CompressionResult(false, ex.Message);
        }
    }

    async Task<TimeSpan> ProbeDurationAsync(string inputPath, CancellationToken cancellationToken)
    {
        TimeSpan duration = TimeSpan.Zero;

        // ffmpeg -i input -hide_banner always "fails" (no output specified)
        // but it prints file info including duration to stderr
        await Cli.Wrap(_ffmpegPath)
            .WithArguments(["-i", inputPath, "-hide_banner"])
            .WithValidation(CommandResultValidation.None)
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
            {
                // Matches: "  Duration: 00:05:30.12, start: ..."
                Match match = DurationPattern().Match(line);

                if (match.Success && TimeSpan.TryParse(match.Groups[1].Value, out TimeSpan parsed))
                    duration = parsed;
            }))
            .ExecuteAsync(cancellationToken);

        return duration;
    }

    static string[] BuildArguments(string inputPath, string outputPath, CompressionOptions options)
    {
        int crf = CalculateCrf(options.Codec, options.Quality);
        string encoder = GetEncoder(options.Codec);

        List<string> args =
        [
            "-y", // overwrite output without asking
            "-nostdin", // don't read from stdin (prevents hanging)
            "-i", inputPath,
            "-c:v", encoder,
            "-crf", crf.ToString(),
        ];

        // VP9 and AV1 require -b:v 0 for CRF mode
        if (options.Codec is Codec.Vp9 or Codec.Av1)
        {
            args.Add("-b:v");
            args.Add("0");
        }

        args.Add("-c:a");
        args.Add("copy"); // keep audio as-is
        args.Add(outputPath);

        return args.ToArray();
    }

    static string GetEncoder(Codec codec) => codec switch
    {
        Codec.H264 => "libx264",
        Codec.H265 => "libx265",
        Codec.Vp9 => "libvpx-vp9",
        Codec.Av1 => "libaom-av1",
        _ => throw new ArgumentOutOfRangeException(nameof(codec)),
    };

    static int CalculateCrf(Codec codec, int quality)
    {
        // Quality is 0-100 (higher = better), CRF is inverted (lower = better)
        int maxCrf = codec is Codec.Vp9 or Codec.Av1 ? 63 : 51;
        return maxCrf - (int)(maxCrf * quality / 100.0);
    }

    static TimeSpan? ParseTime(string line)
    {
        // Progress lines look like: "frame= 123 fps= 45 ... time=00:02:15.50 ..."
        Match match = TimePattern().Match(line);

        if (match.Success && TimeSpan.TryParse(match.Groups[1].Value, out TimeSpan parsed))
            return parsed;

        return null;
    }

    [GeneratedRegex(@"Duration:\s*(\d+:\d+:\d+\.\d+)")]
    private static partial Regex DurationPattern();

    [GeneratedRegex(@"time=(\d+:\d+:\d+\.\d+)")]
    private static partial Regex TimePattern();
}
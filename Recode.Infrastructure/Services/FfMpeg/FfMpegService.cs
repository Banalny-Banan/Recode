using System.Text.RegularExpressions;
using CliWrap;
using Recode.Core.Enums;
using Recode.Core.Services.Ffmpeg;

namespace Recode.Infrastructure.Services.FfMpeg;

public partial class FfMpegService(IFfmpegManager ffmpegManager) : IFfMpegService
{
    // GPU encoders ordered by priority: NVENC > AMF > QSV
    static readonly (string encoder, string vendor)[][] GpuEncoders =
    [
        // None — no encoding
        [],
        // H.264
        [("h264_nvenc", "nvenc"), ("h264_amf", "amf"), ("h264_qsv", "qsv")],
        // H.265
        [("hevc_nvenc", "nvenc"), ("hevc_amf", "amf"), ("hevc_qsv", "qsv")],
        // VP9 — no GPU encoders
        [],
    ];

    // Keep only the last N stderr lines; enough to surface error context without unbounded growth
    const int MaxStderrLines = 20;

    readonly string _ffmpegPath = ffmpegManager.FfmpegPath;
    HashSet<string>? _availableEncoders;

    public async Task<FfMpegResult> CompressAsync
    (
        string inputPath,
        string outputPath,
        FfMpegOptions options,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        try
        {
            if (options.UseGpu)
                await EnsureEncodersCached(cancellationToken);

            TimeSpan duration = await ProbeDurationAsync(inputPath, cancellationToken);

            if (duration <= TimeSpan.Zero)
                return new FfMpegResult(false, "Could not determine file duration");

            var lastStderrLine = "";
            Queue<string> stderrLines = new();

            CommandResult result = await Cli.Wrap(_ffmpegPath)
                .WithArguments(BuildArguments(inputPath, outputPath, options))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
                {
                    lastStderrLine = line;
                    stderrLines.Enqueue(line);
                    if (stderrLines.Count > MaxStderrLines)
                        stderrLines.Dequeue();

                    TimeSpan? currentTime = ParseTime(line);

                    if (currentTime.HasValue)
                        progress.Report(currentTime.Value / duration * 100);
                }))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync(cancellationToken);

            if (result.ExitCode != 0)
            {
                string errorMsg = string.Join("\n", stderrLines.Where(l => !string.IsNullOrWhiteSpace(l)));
                return new FfMpegResult(false, string.IsNullOrEmpty(errorMsg) ? lastStderrLine : errorMsg);
            }

            return new FfMpegResult(true, null);
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);

            return new FfMpegResult(false, "Compression was cancelled");
        }
        catch (Exception ex)
        {
            return new FfMpegResult(false, $"An error occurred: {ex.Message}");
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

    string[] BuildArguments(string inputPath, string outputPath, FfMpegOptions options)
    {
        List<string> args =
        [
            "-y", // overwrite output without asking
            "-nostdin", // don't read from stdin (prevents hanging)
            "-stats_period", "0.1", // update progress 10 times per second
            "-i", inputPath,
        ];

        int crf = CalculateCrf(options.Codec, options.Quality);
        string? gpuEncoder = options.UseGpu ? FindGpuEncoder(options.Codec) : null;
        string encoder = gpuEncoder ?? GetSoftwareEncoder(options.Codec);

        args.Add("-c:v");
        args.Add(encoder);

        if (gpuEncoder != null)
            AddGpuQualityArgs(args, gpuEncoder, crf);
        else
        {
            args.Add("-crf");
            args.Add(crf.ToString());

            // VP9 requires -b:v 0 for CRF mode
            if (options.Codec is Codec.Vp9)
            {
                args.Add("-b:v");
                args.Add("0");
            }
        }

        args.Add("-c:a");
        args.Add("copy");

        args.Add(outputPath);

        return args.ToArray();
    }

    static string GetSoftwareEncoder(Codec codec) => codec switch
    {
        Codec.H264 => "libx264",
        Codec.H265 => "libx265",
        Codec.Vp9 => "libvpx-vp9",
        _ => throw new ArgumentOutOfRangeException(nameof(codec)),
    };

    string? FindGpuEncoder(Codec codec)
    {
        if (_availableEncoders is null)
            return null;

        (string encoder, string vendor)[] candidates = GpuEncoders[(int)codec];

        foreach ((string encoder, _) in candidates)
        {
            if (_availableEncoders.Contains(encoder))
                return encoder;
        }

        return null;
    }

    static void AddGpuQualityArgs(List<string> args, string encoder, int crf)
    {
        if (encoder.Contains("nvenc"))
        {
            args.Add("-cq");
            args.Add(crf.ToString());
        }
        else if (encoder.Contains("amf"))
        {
            args.Add("-rc");
            args.Add("cqp");
            args.Add("-qp_i");
            args.Add(crf.ToString());
            args.Add("-qp_p");
            args.Add(crf.ToString());
        }
        else if (encoder.Contains("qsv"))
        {
            args.Add("-global_quality");
            args.Add(crf.ToString());
        }
    }

    async Task EnsureEncodersCached(CancellationToken cancellationToken)
    {
        if (_availableEncoders != null)
            return;

        var encoders = new HashSet<string>(StringComparer.Ordinal);

        await Cli.Wrap(_ffmpegPath)
            .WithArguments(["-encoders", "-hide_banner"])
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line =>
            {
                // Lines look like: " V..... h264_nvenc  NVIDIA NVENC H.264 encoder (codec h264)"
                string trimmed = line.TrimStart();

                if (!trimmed.StartsWith("V"))
                    return;

                string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length >= 2)
                    encoders.Add(parts[1]);
            }))
            .ExecuteAsync(cancellationToken);

        _availableEncoders = encoders;
    }

    static int CalculateCrf(Codec codec, int quality)
    {
        // Quality is 0-100 (higher = better), CRF is inverted (lower = better)
        int maxCrf = codec == Codec.Vp9 ? 63 : 51;
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
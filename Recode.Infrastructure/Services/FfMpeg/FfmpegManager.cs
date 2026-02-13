using System.IO.Compression;
using Recode.Core.Services.FfmpegManager;

namespace Recode.Infrastructure.Services.FfMpeg;

public class FfmpegManager : IFfmpegManager
{
    static readonly string FfmpegDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Recode");
    
    public string FfmpegPath { get; } = Path.Combine(FfmpegDir, "ffmpeg.exe");

    public async Task<(bool Success, string? Message)> EnsureAvailableAsync(IProgress<double> progress, CancellationToken cancellationToken = default)
    {
        var tempZip = string.Empty;

        try
        {
            if (File.Exists(FfmpegPath))
                return (true, null);

            using HttpClient client = new();
            tempZip = Path.GetTempFileName();

            using HttpResponseMessage response = await client.GetAsync
            (
                "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
                return (false, response.ReasonPhrase);

            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            long bytesRead = 0;

            await using FileStream fileStream = File.Create(tempZip);
            await using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var buffer = new byte[81920];
            int read;

            while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                bytesRead += read;

                if (totalBytes > 0)
                    progress?.Report((double)bytesRead / totalBytes * 100);
            }

            fileStream.Close();

            // Extract only ffmpeg.exe from the zip
            Directory.CreateDirectory(Path.GetDirectoryName(FfmpegPath)!);

            using ZipArchive archive = ZipFile.OpenRead(tempZip);
            ZipArchiveEntry? entry = archive.Entries.FirstOrDefault(e => e.Name == "ffmpeg.exe");

            if (entry == null)
                return (false, "ffmpeg.exe not found in the downloaded archive.");

            entry.ExtractToFile(FfmpegPath, true);
        }
        catch (Exception ex)
        {
            return (false, $"Unhandled exception: {ex.Message}");
        }
        finally
        {
            if (File.Exists(tempZip))
                File.Delete(tempZip);
        }

        return (true, null);
    }
}
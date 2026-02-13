using System.IO.Compression;

namespace Recode.Core.Services.FfmpegManager;

public class FfmpegManager : IFfmpegManager
{
    public string FfmpegPath { get; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");

    public async Task<(bool Success, string? Message)> EnsureAvailableAsync(IProgress<double> progress)
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
                HttpCompletionOption.ResponseHeadersRead
            );

            if (!response.IsSuccessStatusCode)
                return (false, $"Failed to download ffmpeg: {response.ReasonPhrase}");

            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            long bytesRead = 0;

            await using FileStream fileStream = File.Create(tempZip);
            await using Stream contentStream = await response.Content.ReadAsStreamAsync();

            var buffer = new byte[81920];
            int read;

            while ((read = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
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
            return (false, $"Failed to download ffmpeg: {ex.Message}");
        }
        finally
        {
            if (File.Exists(tempZip))
                File.Delete(tempZip);
        }

        return (true, null);
    }
}
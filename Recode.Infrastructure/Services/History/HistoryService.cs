using System.Security.Cryptography;
using System.Text.Json;
using Recode.Core.Services.History;

namespace Recode.Infrastructure.Services.History;

public class HistoryService : IHistoryService
{
    const int ChunkSize = 65536; // 64KB
    static readonly string HistoryDir = AppPaths.AppDataDir;
    static readonly string HistoryFile = Path.Combine(HistoryDir, "history.json");

    readonly SemaphoreSlim _lock = new(1, 1);
    readonly HashSet<string> _hashes;

    public HistoryService() => _hashes = LoadFromDisk();

    public async Task<bool> IsCompressedAsync(string filePath)
    {
        string hash = await ComputeHashAsync(filePath);
        return _hashes.Contains(hash);
    }

    public async Task RecordCompressedAsync(string filePath)
    {
        string hash = await ComputeHashAsync(filePath);

        await _lock.WaitAsync();
        try
        {
            if (_hashes.Add(hash))
                await SaveAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    static async Task<string> ComputeHashAsync(string filePath)
    {
        await using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkSize, useAsync: true);
        long fileSize = fs.Length;

        // Hash: file size + first 64KB + last 64KB
        using var sha = SHA256.Create();
        byte[] sizeBytes = BitConverter.GetBytes(fileSize);
        sha.TransformBlock(sizeBytes, 0, sizeBytes.Length, null, 0);

        var buffer = new byte[ChunkSize];

        // First chunk
        int firstRead = await fs.ReadAsync(buffer);
        sha.TransformBlock(buffer, 0, firstRead, null, 0);

        // Last chunk (if file is large enough that it's different from the first)
        if (fileSize > ChunkSize)
        {
            fs.Seek(-ChunkSize, SeekOrigin.End);
            int lastRead = await fs.ReadAsync(buffer);
            sha.TransformBlock(buffer, 0, lastRead, null, 0);
        }

        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!);
    }

    static HashSet<string> LoadFromDisk()
    {
        try
        {
            if (!File.Exists(HistoryFile))
                return [];

            string json = File.ReadAllText(HistoryFile);
            return JsonSerializer.Deserialize<HashSet<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    async Task SaveAsync()
    {
        Directory.CreateDirectory(HistoryDir);
        string json = JsonSerializer.Serialize(_hashes);
        await File.WriteAllTextAsync(HistoryFile, json);
    }
}

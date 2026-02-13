using System.Security.Cryptography;
using System.Text.Json;
using Recode.Core.Services.History;

namespace Recode.Infrastructure.Services.History;

public class HistoryService : IHistoryService
{
    const int ChunkSize = 65536; // 64KB
    static readonly string HistoryDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Recode");
    static readonly string HistoryFile = Path.Combine(HistoryDir, "history.json");

    readonly HashSet<string> _hashes;

    public HistoryService() => _hashes = Load();

    public bool IsCompressed(string filePath)
    {
        string hash = ComputeHash(filePath);
        return _hashes.Contains(hash);
    }

    public void RecordCompressed(string filePath)
    {
        string hash = ComputeHash(filePath);

        if (_hashes.Add(hash))
            Save();
    }

    static string ComputeHash(string filePath)
    {
        using FileStream fs = File.OpenRead(filePath);
        long fileSize = fs.Length;

        // Hash: file size + first 64KB + last 64KB
        using var sha = SHA256.Create();
        byte[] sizeBytes = BitConverter.GetBytes(fileSize);
        sha.TransformBlock(sizeBytes, 0, sizeBytes.Length, null, 0);

        var buffer = new byte[ChunkSize];

        // First chunk
        int firstRead = fs.Read(buffer, 0, ChunkSize);
        sha.TransformBlock(buffer, 0, firstRead, null, 0);

        // Last chunk (if file is large enough that it's different from the first)
        if (fileSize > ChunkSize)
        {
            fs.Seek(-ChunkSize, SeekOrigin.End);
            int lastRead = fs.Read(buffer, 0, ChunkSize);
            sha.TransformBlock(buffer, 0, lastRead, null, 0);
        }

        sha.TransformFinalBlock([], 0, 0);
        string result = Convert.ToHexString(sha.Hash!);
        return result;
    }

    HashSet<string> Load()
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

    void Save()
    {
        Directory.CreateDirectory(HistoryDir);
        string json = JsonSerializer.Serialize(_hashes);
        File.WriteAllText(HistoryFile, json);
    }
}
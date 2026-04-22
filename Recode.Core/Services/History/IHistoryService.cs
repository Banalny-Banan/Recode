namespace Recode.Core.Services.History;

public interface IHistoryService
{
    Task<bool> IsCompressedAsync(string filePath);
    Task RecordCompressedAsync(string filePath);
}
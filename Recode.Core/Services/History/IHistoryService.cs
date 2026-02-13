namespace Recode.Core.Services.History;

public interface IHistoryService
{
    bool IsCompressed(string filePath);
    void RecordCompressed(string filePath);
}
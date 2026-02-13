namespace Recode.Infrastructure.Services.History;

public class HistoryService
{
    static readonly string HistoryDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Recode");
    static readonly string HistoryFile = Path.Combine(HistoryDir, "history.json");

    public bool IsCompressed(string filePath) { }

    public void RecordCompressed(string filePath) { }
}
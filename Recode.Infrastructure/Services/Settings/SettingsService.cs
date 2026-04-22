using System.Text.Json;
using Recode.Core.Services.Settings;

namespace Recode.Infrastructure.Services.Settings;

public class SettingsService : ISettingsService
{
    static readonly string SettingsDir = AppPaths.AppDataDir;
    static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch
        {
            // Corrupted file — return defaults
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDir);
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsFile, json);
    }
}
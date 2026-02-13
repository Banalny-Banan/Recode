namespace Recode.Core.Services.Settings;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
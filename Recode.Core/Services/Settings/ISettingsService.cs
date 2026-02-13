using Recode.Core.Enums;

namespace Recode.Core.Services.Settings;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}

public record AppSettings(Codec SelectedCodec, int QualityValue, string OutputPath, bool ReplaceFiles, bool UseGpu, AfterCompletionAction AfterCompletionAction)
{
    public AppSettings() : this(Codec.H264, 50, "", false, false, AfterCompletionAction.Nothing) { }
}
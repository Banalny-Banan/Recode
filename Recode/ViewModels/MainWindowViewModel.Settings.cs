using Recode.Core;
using Recode.Core.Services.Settings;

namespace Recode.ViewModels;

public partial class MainWindowViewModel
{
    readonly ISettingsService? _settingsService;

    partial void OnSelectedCodecChanged(Codec value) => SaveSettings();
    partial void OnQualityValueChanged(int value) => SaveSettings();
    partial void OnReplaceFilesChanged(bool value) => SaveSettings();
    partial void OnOutputPathChanged(string value) => SaveSettings();
    partial void OnAfterCompletionActionChanged(AfterCompletionAction value) => SaveSettings();

    void SaveSettings()
    {
        if (_settingsService == null)
            return;

        AppSettings settings = new()
        {
            SelectedCodec = SelectedCodec,
            QualityValue = QualityValue,
            OutputPath = OutputPath,
            ReplaceFiles = ReplaceFiles,
            AfterCompletionAction = AfterCompletionAction,
        };

        _settingsService.Save(settings);
    }
}
namespace Recode.Core.Services.Settings;

public record AppSettings(Codec SelectedCodec, int QualityValue, string OutputPath, bool ReplaceFiles, AfterCompletionAction AfterCompletionAction)
{
    public AppSettings() : this(Codec.H264, 50, "", false, AfterCompletionAction.Nothing) { }
}
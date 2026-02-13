using System.ComponentModel;

namespace Recode.Core.Enums;

public enum AfterCompletionAction
{
    [Description("Do nothing")]
    Nothing,
    [Description("Shut down the computer")]
    Shutdown,
    [Description("Sleep the computer")]
    Sleep,
}
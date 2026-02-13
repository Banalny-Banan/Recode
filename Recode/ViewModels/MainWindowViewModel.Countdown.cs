using System.Threading.Tasks;
using Recode.Core.Enums;
using Recode.Core.Services.Power;
using Recode.Views;

namespace Recode.ViewModels;

public partial class MainWindowViewModel
{
    readonly IPowerService? _powerService;

    async Task ExecuteAfterCompletionAction()
    {
        if (AfterCompletionAction == AfterCompletionAction.Nothing || _powerService is null)
            return;

        string action = AfterCompletionAction == AfterCompletionAction.Shutdown
            ? "Shutting down"
            : "Sleeping";

        bool completed = await AppDialog.ShowCountdown(action, 20);

        if (!completed)
            return;

        if (AfterCompletionAction == AfterCompletionAction.Shutdown)
            _powerService.Shutdown();
        else
            _powerService.Sleep();
    }
}

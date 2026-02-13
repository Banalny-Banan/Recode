using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recode.Core.Enums;
using Recode.Core.Services.Power;

namespace Recode.ViewModels;

public partial class MainWindowViewModel
{
    readonly IPowerService? _powerService;
    CancellationTokenSource? _countdownCts;

    [ObservableProperty]
    bool _countdownVisible;

    [ObservableProperty]
    int _countdownSeconds;

    [ObservableProperty]
    string _countdownMessage = "";

    [RelayCommand]
    void CancelCountdown()
    {
        _countdownCts?.Cancel();
        CountdownVisible = false;
    }

    async Task ExecuteAfterCompletionAction()
    {
        if (AfterCompletionAction == AfterCompletionAction.Nothing || _powerService is null)
            return;

        string action = AfterCompletionAction == AfterCompletionAction.Shutdown
            ? "Shutting down"
            : "Sleeping";

        CountdownVisible = true;
        _countdownCts = new CancellationTokenSource();

        try
        {
            for (var i = 20; i > 0; i--)
            {
                CountdownSeconds = i;
                CountdownMessage = $"{action} in {i} seconds...";
                await Task.Delay(1000, _countdownCts.Token);
            }

            CountdownVisible = false;

            if (AfterCompletionAction == AfterCompletionAction.Shutdown)
                _powerService.Shutdown();
            else
                _powerService.Sleep();
        }
        catch (OperationCanceledException)
        {
            // User cancelled the countdown
        }
        finally
        {
            CountdownVisible = false;
            _countdownCts?.Dispose();
            _countdownCts = null;
        }
    }
}
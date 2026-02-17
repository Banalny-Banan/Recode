using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Recode.ViewModels;

namespace Recode.Views;

public partial class QueueItemView : UserControl
{
    CancellationTokenSource? _copyFeedbackCts;

    public QueueItemView()
    {
        InitializeComponent();
    }

    async void CopyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not QueueItemViewModel { OutputFilePath: string outputFilePath })
            return;

        var topLevel = TopLevel.GetTopLevel(this);

        if (topLevel?.Clipboard is not { } clipboard)
            return;

        IStorageFile? file = await topLevel.StorageProvider.TryGetFileFromPathAsync(outputFilePath);

        if (file is null)
            return;

        await clipboard.SetFilesAsync([file]);

        if (sender is not Button button)
            return;

        await PlayCopiedAnimation(button);
    }

    async Task PlayCopiedAnimation(Button button)
    {
        if (_copyFeedbackCts != null)
        {
            await _copyFeedbackCts.CancelAsync();
            _copyFeedbackCts.Dispose();
        }

        _copyFeedbackCts = new CancellationTokenSource();

        button.Classes.Add("copied");

        try
        {
            await Task.Delay(1000, _copyFeedbackCts.Token);
            button.Classes.Remove("copied");
        }
        catch (OperationCanceledException) { }
    }
}
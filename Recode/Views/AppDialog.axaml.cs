using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Recode.Views;

public partial class AppDialog : Window
{
    bool _result;

    AppDialog()
    {
        InitializeComponent();
    }

    static Window? GetMainWindow()
        => (App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public static async Task ShowError(string title, string message)
    {
        AppDialog dialog = new() { Title = title };
        dialog.MessageText.Text = message;
        dialog.PrimaryButton.Content = "OK";
        dialog.PrimaryButton.IsVisible = true;
        dialog.PrimaryButton.Click += (_, _) => dialog.Close();

        Window? owner = GetMainWindow();

        if (owner != null)
            await dialog.ShowDialog(owner);
    }

    public static async Task<bool> AskYesNo(string title, string message)
    {
        AppDialog dialog = new() { Title = title };
        dialog.MessageText.Text = message;
        dialog.PrimaryButton.Content = "Yes";
        dialog.PrimaryButton.IsVisible = true;

        dialog.PrimaryButton.Click += (_, _) =>
        {
            dialog._result = true;
            dialog.Close();
        };
        dialog.SecondaryButton.Content = "No";
        dialog.SecondaryButton.IsVisible = true;

        dialog.SecondaryButton.Click += (_, _) =>
        {
            dialog._result = false;
            dialog.Close();
        };

        Window? owner = GetMainWindow();

        if (owner != null)
            await dialog.ShowDialog(owner);

        return dialog._result;
    }

    public static async Task<bool> ShowCountdown(string action, int seconds)
    {
        AppDialog dialog = new()
        {
            Title = action,
            Topmost = true,
        };

        dialog.PrimaryButton.Content = "Cancel";
        dialog.PrimaryButton.IsVisible = true;

        dialog.PrimaryButton.Click += (_, _) =>
        {
            dialog._result = false;
            dialog.Close();
        };

        CancellationTokenSource cts = new();
        dialog.Closed += (_, _) => cts.Cancel();

        Window? owner = GetMainWindow();

        if (owner != null)
        {
            if (owner.WindowState == WindowState.Minimized)
                owner.WindowState = WindowState.Normal;
            owner.Activate();
        }

        _ = RunCountdown(dialog, action, seconds, cts.Token);

        if (owner != null)
            await dialog.ShowDialog(owner);

        cts.Dispose();
        return dialog._result;
    }

    static async Task RunCountdown(AppDialog dialog, string action, int seconds, CancellationToken ct)
    {
        try
        {
            for (int i = seconds; i > 0; i--)
            {
                dialog.MessageText.Text = $"{action} in {i} seconds...";
                await Task.Delay(1000, ct);
            }

            dialog._result = true;
            dialog.Close();
        }
        catch (OperationCanceledException)
        {
            // Dialog was closed or cancelled
        }
    }
}
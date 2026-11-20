using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PCRemote.Desktop.ViewModels.Pages;

namespace PCRemote.Desktop.Views.Pages;

public partial class AboutPage : UserControl
{
    private readonly DispatcherTimer _copyFeedbackTimer = new()
    {
        Interval = TimeSpan.FromSeconds(3)
    };

    public AboutPage()
    {
        InitializeComponent();

        _copyFeedbackTimer.Tick += CopyFeedbackTimer_Tick;
    }

    private async void OnCopyRepositoryClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (DataContext is not AboutPageViewModel viewModel)
            return;

        await CopyAsync(
            viewModel.RepositoryUrl,
            RepositoryCopyFeedback);
    }

    private async void OnCopyTelegramClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (DataContext is not AboutPageViewModel viewModel)
            return;

        await CopyAsync(
            viewModel.TelegramUrl,
            TelegramCopyFeedback);
    }

    private async Task CopyAsync(string text, Panel feedback)
    {
        var topLevel = TopLevel.GetTopLevel(this);

        if (topLevel?.Clipboard is null)
            return;

        await topLevel.Clipboard.SetTextAsync(text);

        ShowCopyFeedback(feedback);
    }

    private void ShowCopyFeedback(Panel feedback)
    {
        ResetCopyFeedback();

        feedback.Classes.Add("copied");

        _copyFeedbackTimer.Start();
    }

    private void CopyFeedbackTimer_Tick(
        object? sender,
        EventArgs e)
    {
        ResetCopyFeedback();
    }

    private void ResetCopyFeedback()
    {
        _copyFeedbackTimer.Stop();

        RepositoryCopyFeedback.Classes.Remove("copied");
        TelegramCopyFeedback.Classes.Remove("copied");
    }

    private async void OnGitHubClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (DataContext is not AboutPageViewModel viewModel)
            return;

        await OpenUrlAsync(viewModel.RepositoryUrl);
    }

    private async void OnTelegramClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (DataContext is not AboutPageViewModel viewModel)
            return;

        await OpenUrlAsync(viewModel.TelegramUrl);
    }

    private static Task OpenUrlAsync(string url)
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
        }
        catch (Exception)
        {
            // TODO: Add error notification if needed.
        }

        return Task.CompletedTask;
    }

    protected override void OnDetachedFromVisualTree(
        VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        ResetCopyFeedback();
    }
}

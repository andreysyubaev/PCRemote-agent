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
            RepositoryCopyIcon,
            RepositoryCopySuccessIcon);
    }

    private async void OnCopyTelegramClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (DataContext is not AboutPageViewModel viewModel)
            return;

        await CopyAsync(
            viewModel.TelegramUrl,
            TelegramCopyIcon,
            TelegramCopySuccessIcon);
    }

    private async Task CopyAsync(
        string text,
        Control icon,
        Control successIcon)
    {
        var topLevel = TopLevel.GetTopLevel(this);

        if (topLevel?.Clipboard is null)
            return;

        await topLevel.Clipboard.SetTextAsync(text);

        ShowCopyFeedback(icon, successIcon);
    }

    private void ShowCopyFeedback(
        Control icon,
        Control successIcon)
    {
        ResetCopyFeedback();

        icon.IsVisible = false;
        successIcon.IsVisible = true;

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

        RepositoryCopyIcon.IsVisible = true;
        RepositoryCopySuccessIcon.IsVisible = false;

        TelegramCopyIcon.IsVisible = true;
        TelegramCopySuccessIcon.IsVisible = false;
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

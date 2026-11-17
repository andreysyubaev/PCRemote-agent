using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PCRemote.Desktop.ViewModels.Pages;

namespace PCRemote.Desktop.Views.Pages;

public partial class SettingsPage : UserControl
{
    private readonly DispatcherTimer _copyFeedbackTimer = new()
    {
        Interval = TimeSpan.FromSeconds(3)
    };

    public SettingsPage()
    {
        InitializeComponent();

        _copyFeedbackTimer.Tick += CopyFeedbackTimer_Tick;
    }

    private async void OnCopyPairingCodeClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (DataContext is not SettingsPageViewModel viewModel)
            return;

        var pairingCode = viewModel.AgentStatus.PairingCode;

        if (string.IsNullOrWhiteSpace(pairingCode))
            return;

        var topLevel = TopLevel.GetTopLevel(this);

        if (topLevel?.Clipboard is null)
            return;

        await topLevel.Clipboard.SetTextAsync(pairingCode);

        ShowCopyFeedback();
    }

    private void ShowCopyFeedback()
    {
        CopyIcon.IsVisible = false;
        CopySuccessIcon.IsVisible = true;

        _copyFeedbackTimer.Stop();
        _copyFeedbackTimer.Start();
    }

    private void CopyFeedbackTimer_Tick(object? sender, EventArgs e)
    {
        ResetCopyFeedback();
    }

    private void ResetCopyFeedback()
    {
        _copyFeedbackTimer.Stop();

        CopyIcon.IsVisible = true;
        CopySuccessIcon.IsVisible = false;
    }

    protected override void OnDetachedFromVisualTree(
        VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        ResetCopyFeedback();
    }
}

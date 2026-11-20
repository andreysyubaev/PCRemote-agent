using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
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

    private SettingsPageViewModel? _highlightSource;
    private int _pairingFlashVersion;

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
        CopyFeedbackPanel.Classes.Add("copied");

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

        CopyFeedbackPanel.Classes.Remove("copied");
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_highlightSource is not null)
            _highlightSource.PairingCodeHighlightRequested -=
                OnPairingCodeHighlightRequested;

        _highlightSource = DataContext as SettingsPageViewModel;

        if (_highlightSource is not null)
            _highlightSource.PairingCodeHighlightRequested +=
                OnPairingCodeHighlightRequested;
    }

    private void OnPairingCodeHighlightRequested()
    {
        FlashPairingCode();
    }

    private async void FlashPairingCode()
    {
        var card = TelegramCard;
        var version = ++_pairingFlashVersion;

        // Резко включаем акцентную рамку всей карточки, без анимации.
        card.Transitions = null;
        card.Classes.Add("flash");

        await Task.Delay(120);

        if (version != _pairingFlashVersion)
            return;

        // Плавно возвращаем обычные цвета рамки и фона.
        card.Transitions = new Transitions
        {
            new BrushTransition
            {
                Property = Border.BorderBrushProperty,
                Duration = TimeSpan.FromSeconds(0.9),
                Easing = new CubicEaseOut()
            },
            new BrushTransition
            {
                Property = Border.BackgroundProperty,
                Duration = TimeSpan.FromSeconds(0.9),
                Easing = new CubicEaseOut()
            }
        };
        card.Classes.Remove("flash");

        await Task.Delay(900);

        if (version != _pairingFlashVersion)
            return;

        card.ClearValue(Animatable.TransitionsProperty);
    }

    protected override void OnDetachedFromVisualTree(
        VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        ResetCopyFeedback();
    }
}

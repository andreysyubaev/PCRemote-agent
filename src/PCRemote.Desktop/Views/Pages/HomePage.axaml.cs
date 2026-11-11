using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PCRemote.Desktop.ViewModels.Pages;

namespace PCRemote.Desktop.Views.Pages;

public partial class HomePage : UserControl
{
    public HomePage()
    {
        InitializeComponent();
    }

    private async void OnCopyPairingCodeClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (DataContext is not HomePageViewModel viewModel)
            return;

        var pairingCode = viewModel.AgentStatus.PairingCode;

        if (string.IsNullOrWhiteSpace(pairingCode))
            return;

        var topLevel = TopLevel.GetTopLevel(this);

        if (topLevel?.Clipboard is null)
            return;

        await topLevel.Clipboard.SetTextAsync(pairingCode);

        viewModel.StatusMessage = "Pairing code copied to clipboard.";
    }
}
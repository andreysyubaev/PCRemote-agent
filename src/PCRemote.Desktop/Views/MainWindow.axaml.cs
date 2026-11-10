using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using PCRemote.Desktop.ViewModels;

namespace PCRemote.Desktop.Views;

public partial class MainWindow : Window
{
    public bool IsExiting { get; set; }
    
    public MainWindow()
    {
        InitializeComponent();
    }
    
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (!IsExiting && !e.Cancel)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private async void CopyPairingCode_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        var code = viewModel.AgentStatus.PairingCode;

        if (string.IsNullOrWhiteSpace(code))
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;

        if (clipboard is null)
            return;

        await clipboard.SetTextAsync(code);
    }
}
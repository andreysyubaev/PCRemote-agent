using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PCRemote.Desktop.Views.Pages;

public partial class GetYourBotTokenPage : UserControl
{
    private const string BotFatherUrl = "https://t.me/BotFather";

    public GetYourBotTokenPage()
    {
        InitializeComponent();
    }

    public event Action? CloseRequested;

    private void OpenBotFather_Click(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(BotFatherUrl)
        {
            UseShellExecute = true
        });
    }

    private void GotIt_Click(object? sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke();
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke();
    }
}

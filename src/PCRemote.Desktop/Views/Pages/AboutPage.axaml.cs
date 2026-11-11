using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PCRemote.Desktop.ViewModels.Pages;

namespace PCRemote.Desktop.Views.Pages;

public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();
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

    private static async Task OpenUrlAsync(string url)
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
    }
}
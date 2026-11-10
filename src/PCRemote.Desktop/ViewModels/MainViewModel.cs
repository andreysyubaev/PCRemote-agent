using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCRemote.Agent.Configuration;

namespace PCRemote.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly TelegramSetupService _telegramSetupService;

    public MainViewModel(TelegramSetupService telegramSetupService)
    {
        _telegramSetupService = telegramSetupService;
    }

    [ObservableProperty]
    public partial string BotToken { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BotUsername { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Enter your Telegram bot token.";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [RelayCommand]
    private async Task ConnectTelegramAsync()
    {
        if (string.IsNullOrWhiteSpace(BotToken))
        {
            StatusMessage = "Please enter your bot token.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Connecting to Telegram...";

            BotUsername = await _telegramSetupService.SetupAsync(BotToken);

            BotToken = string.Empty;
            StatusMessage = $"Connected to @{BotUsername}. Token saved.";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
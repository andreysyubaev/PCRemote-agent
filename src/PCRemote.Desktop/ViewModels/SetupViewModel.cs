using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCRemote.Agent.Configuration;
using PCRemote.Agent.Runtime;

namespace PCRemote.Desktop.ViewModels;

public partial class SetupViewModel : ViewModelBase
{
    private readonly TelegramSetupService _telegramSetupService;
    private readonly AgentRuntime _agentRuntime;

    public SetupViewModel(
        TelegramSetupService telegramSetupService,
        AgentRuntime agentRuntime)
    {
        _telegramSetupService = telegramSetupService;
        _agentRuntime = agentRuntime;
    }

    [ObservableProperty]
    public partial string BotToken { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BotUsername { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; }
        = "Enter your Telegram bot token.";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public event Action? SetupCompleted;

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

            await _agentRuntime.StartAsync();

            BotToken = string.Empty;

            StatusMessage = $"Connected to @{BotUsername}. Token saved.";

            SetupCompleted?.Invoke();
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
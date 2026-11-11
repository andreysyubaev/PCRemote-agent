
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCRemote.Agent.Configuration;
using PCRemote.Agent.Runtime;
using PCRemote.Desktop.Localization;

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

        StatusMessage = LocalizationManager.GetString("Setup_InitialStatus");
    }

    [ObservableProperty]
    public partial string BotToken { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BotUsername { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public event Action? SetupCompleted;

    [RelayCommand]
    private async Task ConnectTelegramAsync()
    {
        if (string.IsNullOrWhiteSpace(BotToken))
        {
            StatusMessage = LocalizationManager.GetString("Setup_EmptyToken");
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = LocalizationManager.GetString("Setup_Connecting");

            BotUsername = await _telegramSetupService.SetupAsync(BotToken);

            await _agentRuntime.StartAsync();

            BotToken = string.Empty;

            StatusMessage = string.Format(
                LocalizationManager.GetString("Setup_Connected"),
                BotUsername);

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

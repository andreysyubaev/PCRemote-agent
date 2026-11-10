using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCRemote.Agent.Configuration;
using PCRemote.Agent.Runtime;
using PCRemote.Core.Models;

namespace PCRemote.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly TelegramSetupService _telegramSetupService;
    private readonly AgentRuntime _agentRuntime;

    public MainViewModel(
        TelegramSetupService telegramSetupService,
        AgentRuntime agentRuntime,
        AgentStatus agentStatus)
    {
        _telegramSetupService = telegramSetupService;
        _agentRuntime = agentRuntime;
        AgentStatus = agentStatus;
    }
    
    public AgentStatus AgentStatus { get; }

    [ObservableProperty]
    public partial string BotToken { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BotUsername { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Enter your Telegram bot token.";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }
    
    [ObservableProperty]
    public partial bool IsSetupRequired { get; set; } = true;

    [ObservableProperty]
    public partial bool IsInitialized { get; set; }
    
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
            IsSetupRequired = false;
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
    
    [RelayCommand]
    private async Task StartAgentAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Starting PCRemote Agent...";

            await _agentRuntime.StartAsync();

            StatusMessage = "PCRemote Agent is running.";
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

    [RelayCommand]
    private async Task StopAgentAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Stopping PCRemote Agent...";

            await _agentRuntime.StopAsync();

            StatusMessage = "PCRemote Agent stopped.";
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
    
    public async Task InitializeAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Checking configuration...";

            var isTokenConfigured =
                await _telegramSetupService.IsTokenConfiguredAsync();

            if (isTokenConfigured)
            {
                StatusMessage = "Starting PCRemote Agent...";

                await _agentRuntime.StartAsync();

                IsSetupRequired = false;
                StatusMessage = "PCRemote Agent is running.";
            }
            else
            {
                IsSetupRequired = true;
                StatusMessage = "Enter your Telegram bot token.";
            }
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
            IsInitialized = true;
        }
    }
}
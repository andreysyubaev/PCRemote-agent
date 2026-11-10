using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCRemote.Agent.Configuration;
using PCRemote.Agent.Runtime;
using PCRemote.Core.Models;
using System.ComponentModel;
using Avalonia.Threading;

namespace PCRemote.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly TelegramSetupService _telegramSetupService;
    private readonly AgentRuntime _agentRuntime;
    private readonly DispatcherTimer _pairingTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    public MainViewModel(
        TelegramSetupService telegramSetupService,
        AgentRuntime agentRuntime,
        AgentStatus agentStatus)
    {
        _telegramSetupService = telegramSetupService;
        _agentRuntime = agentRuntime;
        AgentStatus = agentStatus;
        AgentStatus.PropertyChanged += AgentStatus_PropertyChanged;
        _pairingTimer.Tick += PairingTimer_Tick;

        UpdatePairingTimer();
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
    
    [ObservableProperty]
    public partial string RemainingPairingTime { get; set; } = "05:00";
    
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
    
    private void AgentStatus_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AgentStatus.PairingCodeExpiresAt) ||
            e.PropertyName == nameof(AgentStatus.IsPaired))
        {
            Dispatcher.UIThread.Post(UpdatePairingTimer);
        }
    }

    private void PairingTimer_Tick(object? sender, EventArgs e)
    {
        UpdatePairingTimer();
    }

    private void UpdatePairingTimer()
    {
        if (AgentStatus.IsPaired ||
            AgentStatus.PairingCodeExpiresAt is not DateTimeOffset expiresAt)
        {
            _pairingTimer.Stop();
            RemainingPairingTime = "05:00";
            return;
        }

        var remaining = expiresAt - DateTimeOffset.UtcNow;

        if (remaining <= TimeSpan.Zero)
        {
            RemainingPairingTime = "00:00";
        }
        else
        {
            var totalSeconds = (int)Math.Ceiling(remaining.TotalSeconds);

            RemainingPairingTime =
                $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        _pairingTimer.Start();
    }
}
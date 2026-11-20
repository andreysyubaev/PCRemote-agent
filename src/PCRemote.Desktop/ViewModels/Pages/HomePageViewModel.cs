
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCRemote.Agent.Runtime;
using PCRemote.Core.Interfaces;
using PCRemote.Core.Interfaces.Logging;
using PCRemote.Core.Models;
using PCRemote.Desktop.Localization;

namespace PCRemote.Desktop.ViewModels.Pages;

public partial class HomePageViewModel : ViewModelBase, IDisposable
{
    private readonly AgentRuntime _agentRuntime;
    private readonly IAutoStartService _autoStartService;
    private readonly ILogReader _logReader;
    private readonly ILogWatcher _logWatcher;

    private bool _isInitializingAutoStart;

    private bool _isRefreshingLastEvent;
    private bool _lastEventRefreshRequested;

    public HomePageViewModel(
        AgentRuntime agentRuntime,
        AgentStatus agentStatus,
        IAutoStartService autoStartService,
        ILogReader logReader,
        ILogWatcher logWatcher)
    {
        _agentRuntime = agentRuntime;
        _autoStartService = autoStartService;
        _logReader = logReader;
        _logWatcher = logWatcher;
        AgentStatus = agentStatus;

        _isInitializingAutoStart = true;
        IsAutoStartEnabled = _autoStartService.IsEnabled();
        _isInitializingAutoStart = false;

        AgentStatus.PropertyChanged += AgentStatus_PropertyChanged;
        _logWatcher.LogsChanged += OnLogsChanged;

        _ = RefreshLastEventAsync();
    }

    public AgentStatus AgentStatus { get; }

    public event EventHandler? OpenLogsRequested;
    public event EventHandler? OpenPairingHelpRequested;

    [ObservableProperty]
    public partial string LastEventText { get; set; } =
        LocalizationManager.GetString("Home_NoEvents");

    public bool CanStartAgent => !AgentStatus.IsRunning && !IsBusy;

    public bool CanStopAgent => AgentStatus.IsRunning && !IsBusy;

    public string AgentStateText => LocalizationManager.GetString(
        AgentStatus.IsRunning ? "Home_AgentWorking" : "Home_AgentStopped");

    public string TelegramStatusText => LocalizationManager.GetString(
        AgentStatus.IsTelegramConnected
            ? "Home_Connected"
            : "Home_NotConnected");

    public string AccountStatusText => LocalizationManager.GetString(
        AgentStatus.IsPaired ? "Home_Paired" : "Home_NotPaired");

    public string AccountTitleText => LocalizationManager.GetString(
        AgentStatus.IsPaired ? "Home_AccountPaired" : "Home_Account");

    public string BotUsernameText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(AgentStatus.BotUsername))
                return LocalizationManager.GetString("Home_BotUsernameFallback");

            return AgentStatus.BotUsername.StartsWith('@')
                ? AgentStatus.BotUsername
                : $"@{AgentStatus.BotUsername}";
        }
    }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } =
        LocalizationManager.GetString("Home_StatusReady");

    [ObservableProperty]
    public partial bool IsAutoStartEnabled { get; set; }

    [ObservableProperty]
    public partial string AutoStartStatusMessage { get; set; } = string.Empty;

    public bool HasAutoStartStatusMessage =>
        !string.IsNullOrWhiteSpace(AutoStartStatusMessage);

    partial void OnAutoStartStatusMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasAutoStartStatusMessage));
    }

    partial void OnIsAutoStartEnabledChanged(bool value)
    {
        if (_isInitializingAutoStart)
            return;

        try
        {
            if (value)
            {
                _autoStartService.Enable();
                AutoStartStatusMessage =
                    LocalizationManager.GetString("Home_AutoStartEnabled");
            }
            else
            {
                _autoStartService.Disable();
                AutoStartStatusMessage =
                    LocalizationManager.GetString("Home_AutoStartDisabled");
            }
        }
        catch (Exception exception)
        {
            _isInitializingAutoStart = true;
            IsAutoStartEnabled = !value;
            _isInitializingAutoStart = false;

            AutoStartStatusMessage = string.Format(
                LocalizationManager.GetString("Home_AutoStartError"),
                exception.Message);
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartAgent));
        OnPropertyChanged(nameof(CanStopAgent));
    }

    [RelayCommand]
    private void OpenPairingHelp()
    {
        OpenPairingHelpRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task StartAgentAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = LocalizationManager.GetString(
                "Home_StatusStarting");

            await _agentRuntime.StartAsync();

            StatusMessage = LocalizationManager.GetString(
                "Home_StatusStarted");
        }
        catch (Exception exception)
        {
            StatusMessage = $"{LocalizationManager.GetString("Common_Error")}: {exception.Message}";
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
            StatusMessage = LocalizationManager.GetString(
                "Home_StatusStopping");

            await _agentRuntime.StopAsync();

            StatusMessage = LocalizationManager.GetString(
                "Home_StatusStopped");
        }
        catch (Exception exception)
        {
            StatusMessage = $"{LocalizationManager.GetString("Common_Error")}: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenLogs()
    {
        OpenLogsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnLogsChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() => _ = RefreshLastEventAsync());
    }

    private async Task RefreshLastEventAsync()
    {
        if (_isRefreshingLastEvent)
        {
            _lastEventRefreshRequested = true;
            return;
        }

        _isRefreshingLastEvent = true;

        try
        {
            do
            {
                _lastEventRefreshRequested = false;

                var entries = await _logReader.ReadAllAsync();

                LastEventText = entries.Count == 0
                    ? LocalizationManager.GetString("Home_NoEvents")
                    : $"{entries[^1].Message} {entries[^1].Timestamp:HH:mm}";
            }
            while (_lastEventRefreshRequested);
        }
        catch (Exception)
        {
            LastEventText = LocalizationManager.GetString("Home_NoEvents");
        }
        finally
        {
            _isRefreshingLastEvent = false;
        }
    }

    private void AgentStatus_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AgentStatus.IsRunning))
        {
            OnPropertyChanged(nameof(CanStartAgent));
            OnPropertyChanged(nameof(CanStopAgent));
            OnPropertyChanged(nameof(AgentStateText));
        }

        if (e.PropertyName == nameof(AgentStatus.IsTelegramConnected))
        {
            OnPropertyChanged(nameof(TelegramStatusText));
        }

        if (e.PropertyName == nameof(AgentStatus.BotUsername))
        {
            OnPropertyChanged(nameof(BotUsernameText));
        }

        if (e.PropertyName == nameof(AgentStatus.IsPaired))
        {
            OnPropertyChanged(nameof(AccountStatusText));
            OnPropertyChanged(nameof(AccountTitleText));
        }
    }

    public void Dispose()
    {
        AgentStatus.PropertyChanged -= AgentStatus_PropertyChanged;
        _logWatcher.LogsChanged -= OnLogsChanged;
    }
}

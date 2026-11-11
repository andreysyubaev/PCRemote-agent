using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCRemote.Agent.Configuration;
using PCRemote.Agent.Runtime;
using PCRemote.Core.Interfaces;

namespace PCRemote.Desktop.ViewModels.Pages;

public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly AgentRuntime _agentRuntime;
    private readonly TelegramSetupService _telegramSetupService;
    private readonly IAutoStartService _autoStartService;

    private bool _isInitializingAutoStart;

    public SettingsPageViewModel(
        AgentRuntime agentRuntime,
        TelegramSetupService telegramSetupService,
        IAutoStartService autoStartService)
    {
        _agentRuntime = agentRuntime;
        _telegramSetupService = telegramSetupService;
        _autoStartService = autoStartService;

        IsDarkTheme = Application.Current?.RequestedThemeVariant
            == ThemeVariant.Dark;

        _isInitializingAutoStart = true;
        IsAutoStartEnabled = _autoStartService.IsEnabled();
        _isInitializingAutoStart = false;
    }

    [ObservableProperty]
    public partial bool IsDarkTheme { get; set; }

    [ObservableProperty]
    public partial bool IsAutoStartEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsDisconnectConfirmationVisible { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AutoStartStatusMessage { get; set; } = string.Empty;

    public event Action? DisconnectCompleted;

    partial void OnIsDarkThemeChanged(bool value)
    {
        var app = Application.Current;

        if (app is null)
            return;

        app.RequestedThemeVariant = value
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
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
                AutoStartStatusMessage = "Autostart enabled.";
            }
            else
            {
                _autoStartService.Disable();
                AutoStartStatusMessage = "Autostart disabled.";
            }
        }
        catch (Exception exception)
        {
            _isInitializingAutoStart = true;
            IsAutoStartEnabled = !value;
            _isInitializingAutoStart = false;

            AutoStartStatusMessage =
                $"Autostart error: {exception.Message}";
        }
    }

    [RelayCommand]
    private void ShowDisconnectConfirmation()
    {
        IsDisconnectConfirmationVisible = true;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void CancelDisconnect()
    {
        IsDisconnectConfirmationVisible = false;
    }

    [RelayCommand]
    private async Task ConfirmDisconnectAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Disconnecting Telegram bot...";

            await _agentRuntime.StopAsync();

            await _telegramSetupService.RemoveConfigurationAsync();

            StatusMessage = "Telegram bot disconnected.";
            IsDisconnectConfirmationVisible = false;

            DisconnectCompleted?.Invoke();
        }
        catch (Exception exception)
        {
            StatusMessage = $"Disconnect error: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

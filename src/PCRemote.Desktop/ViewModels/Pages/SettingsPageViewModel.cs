using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCRemote.Agent.Configuration;
using PCRemote.Agent.Runtime;

namespace PCRemote.Desktop.ViewModels.Pages;

public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly AgentRuntime _agentRuntime;
    private readonly TelegramSetupService _telegramSetupService;

    public SettingsPageViewModel(
        AgentRuntime agentRuntime,
        TelegramSetupService telegramSetupService)
    {
        _agentRuntime = agentRuntime;
        _telegramSetupService = telegramSetupService;

        IsDarkTheme = Application.Current?.RequestedThemeVariant
            == ThemeVariant.Dark;
    }

    [ObservableProperty]
    public partial bool IsDarkTheme { get; set; }

    [ObservableProperty]
    public partial bool IsDisconnectConfirmationVisible { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

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

            // Останавливаем агента перед удалением настроек.
            await _agentRuntime.StopAsync();

            // Удаляем токен бота и ID привязанного пользователя.
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
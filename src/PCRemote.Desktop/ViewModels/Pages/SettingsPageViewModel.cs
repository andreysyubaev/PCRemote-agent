using System;
using System.IO;
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
    
    private static string GetThemeSettingsPath()
    {
        var appDataPath = OperatingSystem.IsMacOS()
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support")
            : Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(appDataPath, "PCRemote", "theme.txt");
    }
    
    private static string LoadThemePreference()
    {
        var path = GetThemeSettingsPath();

        if (!File.Exists(path))
            return "Система";

        var savedTheme = File.ReadAllText(path).Trim();

        // Поддержка настроек, сохранённых в старом формате.
        if (bool.TryParse(savedTheme, out var isDark))
            return isDark ? "Тёмная" : "Светлая";

        return savedTheme switch
        {
            "Светлая" => "Светлая",
            "Тёмная" => "Тёмная",
            "Система" => "Система",
            _ => "Система"
        };
    }

    private static void SaveThemePreference(string theme)
    {
        var path = GetThemeSettingsPath();

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        File.WriteAllText(path, theme);
    }

    partial void OnSelectedThemeChanged(string value)
    {
        var app = Application.Current;

        if (app is null)
            return;

        app.RequestedThemeVariant = value switch
        {
            "Светлая" => ThemeVariant.Light,
            "Тёмная" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        try
        {
            SaveThemePreference(value);
        }
        catch (Exception exception)
        {
            StatusMessage = $"Theme save error: {exception.Message}";
        }
    }

    public SettingsPageViewModel(
        AgentRuntime agentRuntime,
        TelegramSetupService telegramSetupService,
        IAutoStartService autoStartService)
    {
        _agentRuntime = agentRuntime;
        _telegramSetupService = telegramSetupService;
        _autoStartService = autoStartService;

        SelectedTheme = LoadThemePreference();

        _isInitializingAutoStart = true;
        IsAutoStartEnabled = _autoStartService.IsEnabled();
        _isInitializingAutoStart = false;
    }

    public string[] ThemeOptions { get; } =
    [
        "Светлая",
        "Тёмная",
        "Система"
    ];

    [ObservableProperty]
    public partial string SelectedTheme { get; set; } = "Система";

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

    // partial void OnIsDarkThemeChanged(bool value)
    // {
    //     var app = Application.Current;
    //
    //     if (app is null)
    //         return;
    //
    //     app.RequestedThemeVariant = value
    //         ? ThemeVariant.Dark
    //         : ThemeVariant.Light;
    //
    //     try
    //     {
    //         SaveThemePreference(value);
    //     }
    //     catch (Exception exception)
    //     {
    //         StatusMessage = $"Theme save error: {exception.Message}";
    //     }
    // }

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

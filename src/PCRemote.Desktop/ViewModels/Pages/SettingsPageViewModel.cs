
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
using PCRemote.Desktop.Localization;

namespace PCRemote.Desktop.ViewModels.Pages;

public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly AgentRuntime _agentRuntime;
    private readonly TelegramSetupService _telegramSetupService;
    private readonly IAutoStartService _autoStartService;

    private readonly string _activeLanguagePreference;

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
            return "System";

        var savedTheme = File.ReadAllText(path).Trim();

        if (bool.TryParse(savedTheme, out var isDark))
            return isDark ? "Dark" : "Light";

        return savedTheme switch
        {
            "Светлая" or "Light" => "Light",
            "Тёмная" or "Dark" => "Dark",
            "Система" or "Системная" or "System" => "System",
            _ => "System"
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
        OnPropertyChanged(nameof(SelectedThemeIndex));

        var app = Application.Current;

        if (app is not null)
        {
            app.RequestedThemeVariant = value switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };
        }

        try
        {
            SaveThemePreference(value);
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(
                LocalizationManager.GetString("Settings_ThemeSaveError"),
                exception.Message);
        }
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedLanguageIndex));

        try
        {
            LocalizationManager.SaveLanguagePreference(value);

            IsLanguageChangePending =
                value != _activeLanguagePreference;
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(
                LocalizationManager.GetString("Settings_LanguageSaveError"),
                exception.Message);
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

        _activeLanguagePreference =
            LocalizationManager.GetLanguagePreference();

        SelectedTheme = LoadThemePreference();
        SelectedLanguage = _activeLanguagePreference;

        _isInitializingAutoStart = true;
        IsAutoStartEnabled = _autoStartService.IsEnabled();
        _isInitializingAutoStart = false;
    }

    public string[] ThemeOptions { get; } =
    [
        LocalizationManager.GetString("Settings_ThemeLight"),
        LocalizationManager.GetString("Settings_ThemeDark"),
        LocalizationManager.GetString("Settings_ThemeSystem")
    ];

    public int SelectedThemeIndex
    {
        get => SelectedTheme switch
        {
            "Light" => 0,
            "Dark" => 1,
            _ => 2
        };
        set
        {
            SelectedTheme = value switch
            {
                0 => "Light",
                1 => "Dark",
                _ => "System"
            };
        }
    }

    public string[] LanguageOptions { get; } =
    [
        LocalizationManager.GetString("Settings_LanguageSystem"),
        LocalizationManager.GetString("Settings_LanguageRussian"),
        LocalizationManager.GetString("Settings_LanguageEnglish")
    ];

    public int SelectedLanguageIndex
    {
        get => SelectedLanguage switch
        {
            "ru" => 1,
            "en" => 2,
            _ => 0
        };
        set
        {
            SelectedLanguage = value switch
            {
                1 => "ru",
                2 => "en",
                _ => "System"
            };
        }
    }

    [ObservableProperty]
    public partial string SelectedTheme { get; set; } = "System";

    [ObservableProperty]
    public partial string SelectedLanguage { get; set; } = "System";

    [ObservableProperty]
    public partial bool IsLanguageChangePending { get; set; }

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
    public event Action? RestartRequested;

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
                    LocalizationManager.GetString("Settings_AutoStartEnabled");
            }
            else
            {
                _autoStartService.Disable();
                AutoStartStatusMessage =
                    LocalizationManager.GetString("Settings_AutoStartDisabled");
            }
        }
        catch (Exception exception)
        {
            _isInitializingAutoStart = true;
            IsAutoStartEnabled = !value;
            _isInitializingAutoStart = false;

            AutoStartStatusMessage = string.Format(
                LocalizationManager.GetString("Settings_AutoStartError"),
                exception.Message);
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

            StatusMessage =
                LocalizationManager.GetString("Settings_Disconnecting");

            await _agentRuntime.StopAsync();

            await _telegramSetupService.RemoveConfigurationAsync();

            StatusMessage =
                LocalizationManager.GetString("Settings_Disconnected");

            IsDisconnectConfirmationVisible = false;

            DisconnectCompleted?.Invoke();
        }
        catch (Exception exception)
        {
            StatusMessage = string.Format(
                LocalizationManager.GetString("Settings_DisconnectError"),
                exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    [RelayCommand]
    private void RestartApplication()
    {
        RestartRequested?.Invoke();
    }
}

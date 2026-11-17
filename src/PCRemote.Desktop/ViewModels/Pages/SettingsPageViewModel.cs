
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCRemote.Agent.Configuration;
using PCRemote.Agent.Runtime;
using PCRemote.Core.Interfaces;
using PCRemote.Core.Models;
using PCRemote.Desktop.Localization;

namespace PCRemote.Desktop.ViewModels.Pages;

public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly AgentRuntime _agentRuntime;
    private readonly TelegramSetupService _telegramSetupService;

    private readonly string _activeLanguagePreference;

    private readonly DispatcherTimer _pairingTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

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

    public SettingsPageViewModel(
        AgentRuntime agentRuntime,
        AgentStatus agentStatus,
        TelegramSetupService telegramSetupService)
    {
        _agentRuntime = agentRuntime;
        _telegramSetupService = telegramSetupService;

        AgentStatus = agentStatus;

        _activeLanguagePreference =
            LocalizationManager.GetLanguagePreference();

        SelectedTheme = LoadThemePreference();
        SelectedLanguage = _activeLanguagePreference;

        ApplyThemeVariant(SelectedTheme);

        AgentStatus.PropertyChanged += AgentStatus_PropertyChanged;
        _pairingTimer.Tick += PairingTimer_Tick;

        UpdatePairingTimer();
    }

    public AgentStatus AgentStatus { get; }

    public bool IsPairingBlockVisible => !AgentStatus.IsPaired;

    public bool IsDisconnectBlockVisible => AgentStatus.IsPaired;

    public bool HasPairingCode =>
        !AgentStatus.IsPaired &&
        !string.IsNullOrWhiteSpace(AgentStatus.PairingCode);

    public string PairingCodeText
    {
        get
        {
            var code = AgentStatus.PairingCode?.Trim();

            if (string.IsNullOrWhiteSpace(code))
                return "— — —";

            return code.Length == 6
                ? $"{code[..3]} {code[3..]}"
                : code;
        }
    }

    public string PairingExpiryText => string.Format(
        LocalizationManager.GetString("Home_PairingExpiresIn"),
        RemainingPairingTime);

    public string TelegramActionTitle => LocalizationManager.GetString(
        IsDisconnectConfirmationVisible
            ? "Settings_ConfirmTitle"
            : "Settings_Telegram");

    public string TelegramActionDescription => LocalizationManager.GetString(
        IsDisconnectConfirmationVisible
            ? "Settings_ConfirmShortDescription"
            : "Settings_DisconnectShortDescription");

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
    public partial bool IsDisconnectConfirmationVisible { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RemainingPairingTime { get; set; } = "05:00";

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public event Action? DisconnectCompleted;
    public event Action? RestartRequested;

    partial void OnSelectedThemeChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedThemeIndex));

        ApplyThemeVariant(value);

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

    private static void ApplyThemeVariant(string theme)
    {
        var app = Application.Current;

        if (app is null)
            return;

        app.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
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

    partial void OnIsDisconnectConfirmationVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(TelegramActionTitle));
        OnPropertyChanged(nameof(TelegramActionDescription));
    }

    partial void OnStatusMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));
    }

    partial void OnRemainingPairingTimeChanged(string value)
    {
        OnPropertyChanged(nameof(PairingExpiryText));
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

            AgentStatus.IsPaired = false;
            AgentStatus.PairingCode = null;
            AgentStatus.PairingCodeExpiresAt = null;
            AgentStatus.BotUsername = null;
            AgentStatus.IsTelegramConnected = false;

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

    private void AgentStatus_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AgentStatus.IsPaired))
        {
            OnPropertyChanged(nameof(IsPairingBlockVisible));
            OnPropertyChanged(nameof(IsDisconnectBlockVisible));
            OnPropertyChanged(nameof(HasPairingCode));
            OnPropertyChanged(nameof(PairingCodeText));

            if (!AgentStatus.IsPaired)
                IsDisconnectConfirmationVisible = false;

            Dispatcher.UIThread.Post(UpdatePairingTimer);
        }

        if (e.PropertyName == nameof(AgentStatus.PairingCode))
        {
            OnPropertyChanged(nameof(HasPairingCode));
            OnPropertyChanged(nameof(PairingCodeText));
            Dispatcher.UIThread.Post(UpdatePairingTimer);
        }

        if (e.PropertyName == nameof(AgentStatus.PairingCodeExpiresAt))
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
            _pairingTimer.Stop();
            return;
        }

        var totalSeconds = (int)Math.Ceiling(remaining.TotalSeconds);

        RemainingPairingTime =
            $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";

        _pairingTimer.Start();
    }
}

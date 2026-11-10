using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PCRemote.Desktop.ViewModels.Pages;

public partial class SettingsPageViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial bool IsDarkTheme { get; set; }

    public SettingsPageViewModel()
    {
        IsDarkTheme = Application.Current?.RequestedThemeVariant
                      == ThemeVariant.Dark;
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        var app = Application.Current;

        if (app is null)
            return;

        app.RequestedThemeVariant = value
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
    }
}
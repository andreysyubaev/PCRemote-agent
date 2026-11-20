using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;

namespace PCRemote.Desktop.Views;

public partial class SetupWindow : Window
{
    public SetupWindow()
    {
        InitializeComponent();
        UpdateThemeIcon();
    }

    private void ToggleTheme_Click(object? sender, RoutedEventArgs e)
    {
        var app = Application.Current;

        if (app is null)
            return;

        var isDarkTheme = app.ActualThemeVariant == ThemeVariant.Dark;

        app.RequestedThemeVariant =
            isDarkTheme ? ThemeVariant.Light : ThemeVariant.Dark;

        UpdateThemeIcon(!isDarkTheme);
    }

    private void UpdateThemeIcon(bool? isDarkTheme = null)
    {
        var isDark = isDarkTheme
            ?? Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

        ThemeOutlineIcon.IsVisible = !isDark;
        ThemeFilledIcon.IsVisible = isDark;
    }

    private void WhereGet_Click(object? sender, RoutedEventArgs e)
    {
        SetupContent.IsVisible = false;
        TokenGuidePage.IsVisible = true;
    }

    private void TokenGuidePage_CloseRequested()
    {
        TokenGuidePage.IsVisible = false;
        SetupContent.IsVisible = true;
    }
}
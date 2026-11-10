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
    }

    private void ToggleTheme_Click(object? sender, RoutedEventArgs e)
    {
        var app = Application.Current;

        if (app is null)
            return;

        app.RequestedThemeVariant =
            app.RequestedThemeVariant == ThemeVariant.Dark
                ? ThemeVariant.Light
                : ThemeVariant.Dark;
    }
}
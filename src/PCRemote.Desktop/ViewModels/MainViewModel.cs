using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCRemote.Desktop.ViewModels.Pages;

namespace PCRemote.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public HomePageViewModel HomePage { get; }
    public SettingsPageViewModel SettingsPage { get; }
    public LogsPageViewModel LogsPage { get; }
    public AboutPageViewModel AboutPage { get; }

    [ObservableProperty]
    public partial object? CurrentPage { get; set; }

    public MainViewModel(
        HomePageViewModel homePage,
        SettingsPageViewModel settingsPage,
        LogsPageViewModel logsPage,
        AboutPageViewModel aboutPage)
    {
        HomePage = homePage;
        SettingsPage = settingsPage;
        LogsPage = logsPage;
        AboutPage = aboutPage;

        CurrentPage = HomePage;
    }

    [RelayCommand]
    private void NavigateHome()
    {
        CurrentPage = HomePage;
    }

    [RelayCommand]
    private void NavigateSettings()
    {
        CurrentPage = SettingsPage;
    }

    [RelayCommand]
    private void NavigateLogs()
    {
        CurrentPage = LogsPage;
    }

    [RelayCommand]
    private void NavigateAbout()
    {
        CurrentPage = AboutPage;
    }
}
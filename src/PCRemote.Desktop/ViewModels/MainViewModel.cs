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

    public bool IsHomeSelected => ReferenceEquals(CurrentPage, HomePage);
    public bool IsSettingsSelected => ReferenceEquals(CurrentPage, SettingsPage);
    public bool IsLogsSelected => ReferenceEquals(CurrentPage, LogsPage);
    public bool IsAboutSelected => ReferenceEquals(CurrentPage, AboutPage);

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

        HomePage.OpenLogsRequested += (_, _) => CurrentPage = LogsPage;
        HomePage.OpenPairingHelpRequested += (_, _) =>
        {
            CurrentPage = SettingsPage;
            SettingsPage.HighlightPairingCode();
        };
    }

    partial void OnCurrentPageChanged(object? value)
    {
        OnPropertyChanged(nameof(IsHomeSelected));
        OnPropertyChanged(nameof(IsSettingsSelected));
        OnPropertyChanged(nameof(IsLogsSelected));
        OnPropertyChanged(nameof(IsAboutSelected));
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
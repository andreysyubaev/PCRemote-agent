using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCRemote.Desktop.ViewModels.Pages;

namespace PCRemote.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public HomePageViewModel HomePage { get; }
    public SettingsPageViewModel SettingsPage { get; }

    [ObservableProperty]
    public partial object? CurrentPage { get; set; }

    public MainViewModel(
        HomePageViewModel homePage,
        SettingsPageViewModel settingsPage)
    {
        HomePage = homePage;
        SettingsPage = settingsPage;

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
}
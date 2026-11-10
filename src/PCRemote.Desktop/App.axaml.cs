using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PCRemote.Agent;
using PCRemote.Agent.Configuration;
using PCRemote.Agent.Runtime;
using PCRemote.Core.Models;
using PCRemote.Desktop.ViewModels;
using PCRemote.Desktop.Views;

namespace PCRemote.Desktop;

public partial class App : Application
{
    private IHost? _host;
    private AgentRuntime? _agentRuntime;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _host = AgentHost.Build([]);

            _agentRuntime = _host.Services.GetRequiredService<AgentRuntime>();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(
                    _host.Services.GetRequiredService<TelegramSetupService>(),
                    _agentRuntime,
                    _host.Services.GetRequiredService<AgentStatus>()),
            };

            desktop.Exit += OnDesktopExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        if (_agentRuntime is not null)
        {
            await _agentRuntime.StopAsync();
        }

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
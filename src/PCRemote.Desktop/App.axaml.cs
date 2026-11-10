using System;
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
using Avalonia.Controls;

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

            var mainViewModel = new MainViewModel(
                _host.Services.GetRequiredService<TelegramSetupService>(),
                _agentRuntime,
                _host.Services.GetRequiredService<AgentStatus>());

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel,
            };
            
            if (TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activatableLifetime)
            {
                activatableLifetime.Activated += (_, args) =>
                {
                    if (args.Kind == ActivationKind.Reopen)
                    {
                        OpenPCRemote_Click(this, EventArgs.Empty);
                    }
                };
            }
            
            desktop.MainWindow.Opened += async (_, _) =>
            {
                await mainViewModel.InitializeAsync();
            };

            desktop.Exit += OnDesktopExit;
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    private void OpenPCRemote_Click(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is { } mainWindow)
        {
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
        }
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
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
using Avalonia.Threading;
using System.Linq;

namespace PCRemote.Desktop;

public partial class App : Application
{
    private IHost? _host;
    private AgentRuntime? _agentRuntime;
    private MainViewModel? _mainViewModel;
    private NativeMenuItem? _startAgentMenuItem;
    private NativeMenuItem? _stopAgentMenuItem;

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

            _mainViewModel = new MainViewModel(
                _host.Services.GetRequiredService<TelegramSetupService>(),
                _agentRuntime,
                _host.Services.GetRequiredService<AgentStatus>());
            
            var trayIcon = TrayIcon.GetIcons(this)?.FirstOrDefault();
            var trayMenu = trayIcon?.Menu;

            _startAgentMenuItem = trayMenu?.Items
                .OfType<NativeMenuItem>()
                .FirstOrDefault(item => item.Header == "Start Agent");

            _stopAgentMenuItem = trayMenu?.Items
                .OfType<NativeMenuItem>()
                .FirstOrDefault(item => item.Header == "Stop Agent");
            
            _mainViewModel.AgentStatus.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(AgentStatus.IsRunning))
                {
                    Dispatcher.UIThread.Post(UpdateTrayMenuState);
                }
            };

            UpdateTrayMenuState();

            desktop.MainWindow = new MainWindow
            {
                DataContext = _mainViewModel,
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

            desktop.MainWindow.Opened += async (_, _) => { await _mainViewModel.InitializeAsync(); };

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

    private async void StartAgent_Click(object? sender, EventArgs e)
    {
        if (_mainViewModel is null)
            return;

        await _mainViewModel.StartAgentCommand.ExecuteAsync(null);
    }

    private async void StopAgent_Click(object? sender, EventArgs e)
    {
        if (_mainViewModel is null)
            return;

        await _mainViewModel.StopAgentCommand.ExecuteAsync(null);
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
    
    private void UpdateTrayMenuState()
    {
        if (_mainViewModel is null)
            return;

        var isRunning = _mainViewModel.AgentStatus.IsRunning;

        if (_startAgentMenuItem is not null)
            _startAgentMenuItem.IsEnabled = !isRunning;

        if (_stopAgentMenuItem is not null)
            _stopAgentMenuItem.IsEnabled = isRunning;
    }
}
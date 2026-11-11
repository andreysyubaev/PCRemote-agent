using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PCRemote.Agent;
using PCRemote.Agent.Configuration;
using PCRemote.Agent.Runtime;
using PCRemote.Core.Interfaces;
using PCRemote.Core.Models;
using PCRemote.Desktop.ViewModels;
using PCRemote.Desktop.ViewModels.Pages;
using PCRemote.Desktop.Views;
using System.Threading;
using System.Runtime.InteropServices;

namespace PCRemote.Desktop;

public partial class App : Application
{
    private IHost? _host;
    private AgentRuntime? _agentRuntime;
    private AgentStatus? _agentStatus;
    private MainViewModel? _mainViewModel;
    private NativeMenuItem? _startAgentMenuItem;
    private NativeMenuItem? _stopAgentMenuItem;
    private bool _startMinimized;
    private Mutex? _instanceMutex;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // приложение может работать без открытых окон
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            
            _startMinimized = desktop.Args.Contains(
                "--minimized",
                StringComparer.OrdinalIgnoreCase);
            
            bool createdNew;

            _instanceMutex = new Mutex(
                initiallyOwned: true,
                name: "PCRemote.Desktop.SingleInstance",
                createdNew: out createdNew);

            if (!createdNew)
            {
                _instanceMutex.Dispose();
                _instanceMutex = null;

                desktop.Shutdown();

                base.OnFrameworkInitializationCompleted();
                return;
            }
            
            _host = AgentHost.Build([]);

            _agentRuntime = _host.Services.GetRequiredService<AgentRuntime>();
            _agentStatus = _host.Services.GetRequiredService<AgentStatus>();
            
            SetDockVisibility(false);
            InitializeTray();

            _agentStatus.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(AgentStatus.IsRunning))
                {
                    Dispatcher.UIThread.Post(UpdateTrayMenuState);
                }
            };

            var setupViewModel = new SetupViewModel(
                _host.Services.GetRequiredService<TelegramSetupService>(),
                _agentRuntime);

            var setupWindow = new SetupWindow
            {
                DataContext = setupViewModel
            };

            desktop.MainWindow = setupWindow;

            setupViewModel.SetupCompleted += () =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ShowMainWindow(desktop, setupWindow);
                });
            };

            setupWindow.Opened += async (_, _) =>
            {
                await InitializeStartupAsync(
                    desktop,
                    setupWindow,
                    setupViewModel);
            };

            if (TryGetFeature(typeof(IActivatableLifetime))
                is IActivatableLifetime activatableLifetime)
            {
                activatableLifetime.Activated += (_, args) =>
                {
                    if (args.Kind == ActivationKind.Reopen)
                    {
                        OpenPCRemote_Click(this, EventArgs.Empty);
                    }
                };
            }

            desktop.Exit += OnDesktopExit;

            UpdateTrayMenuState();
        }

        base.OnFrameworkInitializationCompleted();
    }
    

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(
        IntPtr receiver,
        IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern byte objc_msgSendWithPolicy(
        IntPtr receiver,
        IntPtr selector,
        nint policy);
    
    private static void SetDockVisibility(bool visible)
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var applicationClass = objc_getClass("NSApplication");

        var sharedApplicationSelector =
            sel_registerName("sharedApplication");

        var application = objc_msgSend(
            applicationClass,
            sharedApplicationSelector);

        var activationPolicySelector =
            sel_registerName("setActivationPolicy:");

        // 0 — Regular: приложение отображается в Dock.
        // 1 — Accessory: приложение скрыто из Dock.
        objc_msgSendWithPolicy(
            application,
            activationPolicySelector,
            visible ? 0 : 1);
    }

    private void InitializeTray()
    {
        var trayIcon = TrayIcon.GetIcons(this)?.FirstOrDefault();
        var trayMenu = trayIcon?.Menu;

        _startAgentMenuItem = trayMenu?.Items
            .OfType<NativeMenuItem>()
            .FirstOrDefault(item => item.Header == "Start Agent");

        _stopAgentMenuItem = trayMenu?.Items
            .OfType<NativeMenuItem>()
            .FirstOrDefault(item => item.Header == "Stop Agent");
    }

    private async Task InitializeStartupAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        SetupWindow setupWindow,
        SetupViewModel setupViewModel)
    {
        try
        {
            if (_startMinimized)
            {
                setupWindow.Hide();
            }

            var telegramSetupService =
                _host!.Services.GetRequiredService<TelegramSetupService>();

            var isTokenConfigured =
                await telegramSetupService.IsTokenConfiguredAsync();

            if (!isTokenConfigured)
            {
                setupWindow.Show();
                setupWindow.Activate();
                return;
            }

            try
            {
                await _agentRuntime!.StartAsync();
            }
            catch (Exception exception)
            {
                ShowMainWindow(desktop, setupWindow);

                if (_mainViewModel is not null)
                {
                    _mainViewModel.HomePage.StatusMessage =
                        $"Agent startup error: {exception.Message}";
                }

                return;
            }

            if (_startMinimized)
            {
                InitializeMainWindowInTray(desktop, setupWindow);
            }
            else
            {
                ShowMainWindow(desktop, setupWindow);
            }
        }
        catch (Exception exception)
        {
            setupWindow.Show();
            setupViewModel.StatusMessage =
                $"Startup error: {exception.Message}";
        }
    }
    
    private void ShowMainWindow(
        IClassicDesktopStyleApplicationLifetime desktop,
        SetupWindow setupWindow)
    {
        var mainWindow = CreateMainWindow();

        desktop.MainWindow = mainWindow;

        SetDockVisibility(true);

        mainWindow.Show();
        mainWindow.WindowState = WindowState.Normal;
        mainWindow.Activate();

        setupWindow.Close();

        UpdateTrayMenuState();
    }

    private void InitializeMainWindowInTray(
        IClassicDesktopStyleApplicationLifetime desktop,
        SetupWindow setupWindow)
    {
        var mainWindow = CreateMainWindow();

        desktop.MainWindow = mainWindow;

        setupWindow.Close();

        UpdateTrayMenuState();
    }
    
    private MainWindow CreateMainWindow()
    {
        var homePage = new HomePageViewModel(
            _agentRuntime!,
            _agentStatus!);

        var settingsPage = new SettingsPageViewModel(
            _agentRuntime!,
            _host!.Services.GetRequiredService<TelegramSetupService>(),
            _host.Services.GetRequiredService<IAutoStartService>());
        
        settingsPage.DisconnectCompleted += () =>
        {
            Dispatcher.UIThread.Post(ShowSetupWindowAfterDisconnect);
        };
        
        var logsPage = new LogsPageViewModel(
            _host!.Services.GetRequiredService<PCRemote.Core.Interfaces.Logging.ILogReader>(),
            _host.Services.GetRequiredService<PCRemote.Core.Interfaces.Logging.ILogWatcher>());
        _mainViewModel = new MainViewModel(
            homePage,
            settingsPage,
            logsPage);
        
        var mainWindow = new MainWindow
        {
            DataContext = _mainViewModel
        };

        mainWindow.WindowHidden += () =>
        {
            SetDockVisibility(false);
        };

        return mainWindow;
    }
    
    private void ShowSetupWindowAfterDisconnect()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;
        
        var oldWindow = desktop.MainWindow as MainWindow;

        var setupViewModel = new SetupViewModel(
            _host!.Services.GetRequiredService<TelegramSetupService>(),
            _agentRuntime!);

        var setupWindow = new SetupWindow
        {
            DataContext = setupViewModel
        };

        setupViewModel.SetupCompleted += () =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                ShowMainWindow(desktop, setupWindow);
            });
        };

        desktop.MainWindow = setupWindow;
        setupWindow.Show();
        setupWindow.Activate();

        if (desktop.MainWindow is not null)
        {
            // получаем окно через сохранённую ссылку
        }
        
        if (oldWindow is not null)
        {
            oldWindow.IsExiting = true;
            oldWindow.Close();
        }
    }

    private void OpenPCRemote_Click(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is { } mainWindow)
        {
            SetDockVisibility(true);

            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
        }
    }

    private async void StartAgent_Click(object? sender, EventArgs e)
    {
        if (_mainViewModel is null)
            return;

        await _mainViewModel.HomePage.StartAgentCommand.ExecuteAsync(null);
    }

    private async void StopAgent_Click(object? sender, EventArgs e)
    {
        if (_mainViewModel is null)
            return;

        await _mainViewModel.HomePage.StopAgentCommand.ExecuteAsync(null);
    }

    private async void OnDesktopExit(
        object? sender,
        ControlledApplicationLifetimeExitEventArgs e)
    {
        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        _instanceMutex = null;
        
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
        if (_agentStatus is null)
            return;

        var isRunning = _agentStatus.IsRunning;

        if (_startAgentMenuItem is not null)
            _startAgentMenuItem.IsEnabled = !isRunning;

        if (_stopAgentMenuItem is not null)
            _stopAgentMenuItem.IsEnabled = isRunning;
    }
}

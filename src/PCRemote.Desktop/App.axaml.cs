using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PCRemote.Agent;
using PCRemote.Agent.Configuration;
using PCRemote.Agent.Runtime;
using PCRemote.Core.Interfaces;
using PCRemote.Core.Interfaces.Logging;
using PCRemote.Core.Models;
using PCRemote.Desktop.ViewModels;
using PCRemote.Desktop.ViewModels.Pages;
using PCRemote.Desktop.Views;
using System.Threading;
using System.Runtime.InteropServices;
using PCRemote.Desktop.Localization;

namespace PCRemote.Desktop;

public partial class App : Application
{
    private IHost? _host;

    private AgentRuntime? _agentRuntime;
    private AgentStatus? _agentStatus;

    private MainViewModel? _mainViewModel;

    private NativeMenuItem? _startAgentMenuItem;
    private NativeMenuItem? _stopAgentMenuItem;

    private TrayIcon? _trayIcon;
    private WindowIcon? _agentOnIcon;
    private WindowIcon? _agentOffIcon;

    private bool _startMinimized;
    private bool _restartRequested;
    private bool _shutdownPrepared;

    private Mutex? _instanceMutex;

    public override void Initialize()
    {
        LocalizationManager.Initialize();
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
                initiallyOwned: false,
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
                Dispatcher.UIThread.Post(() => { ShowMainWindow(desktop, setupWindow); });
            };

            setupWindow.Opened += async (_, _) =>
            {
                // macOS paints the bundle icon over an icon set before the app
                // finishes launching, so apply ours once a window is visible.
                SetDockVisibility(true);
                SetDockIcon();

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

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(
        IntPtr receiver,
        IntPtr selector,
        IntPtr argument);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(
        IntPtr receiver,
        IntPtr selector,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string argument);

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

    private static void SetDockIcon()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var iconPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Icons",
            "App",
            "app-icon.icns");

        if (!File.Exists(iconPath))
            return;

        var application = objc_msgSend(
            objc_getClass("NSApplication"),
            sel_registerName("sharedApplication"));

        if (application == IntPtr.Zero)
            return;

        var imagePath = objc_msgSend(
            objc_getClass("NSString"),
            sel_registerName("stringWithUTF8String:"),
            iconPath);

        var image = objc_msgSend(
            objc_msgSend(objc_getClass("NSImage"), sel_registerName("alloc")),
            sel_registerName("initWithContentsOfFile:"),
            imagePath);

        if (image == IntPtr.Zero)
            return;

        objc_msgSend(
            application,
            sel_registerName("setApplicationIconImage:"),
            image);

        objc_msgSend(image, sel_registerName("release"));
    }

    private void InitializeTray()
    {
        _trayIcon = TrayIcon.GetIcons(this)?.FirstOrDefault();

        var trayMenu = _trayIcon?.Menu;

        _startAgentMenuItem = trayMenu?.Items
            .OfType<NativeMenuItem>()
            .FirstOrDefault(item => item.Header == "Start Agent");

        _stopAgentMenuItem = trayMenu?.Items
            .OfType<NativeMenuItem>()
            .FirstOrDefault(item => item.Header == "Stop Agent");

        _agentOnIcon = LoadWindowIcon(
            "avares://PCRemote.Desktop/Assets/Icons/App/tray-agent-on.png");

        _agentOffIcon = LoadWindowIcon(
            "avares://PCRemote.Desktop/Assets/Icons/App/tray-agent-off.png");
    }

    private static WindowIcon LoadWindowIcon(string uri)
    {
        using var stream = AssetLoader.Open(new Uri(uri));

        return new WindowIcon(stream);
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
                SetDockVisibility(false);
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
        SetDockIcon();
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
        var logReader = _host!.Services.GetRequiredService<ILogReader>();
        var logWatcher = _host.Services.GetRequiredService<ILogWatcher>();

        var homePage = new HomePageViewModel(
            _agentRuntime!,
            _agentStatus!,
            _host.Services.GetRequiredService<IAutoStartService>(),
            logReader,
            logWatcher);

        var settingsPage = new SettingsPageViewModel(
            _agentRuntime!,
            _agentStatus!,
            _host!.Services.GetRequiredService<TelegramSetupService>());

        settingsPage.RestartRequested += RequestRestart;

        settingsPage.DisconnectCompleted += () => { Dispatcher.UIThread.Post(ShowSetupWindowAfterDisconnect); };

        var logsPage = new LogsPageViewModel(
            logReader,
            logWatcher);

        var aboutPage = new AboutPageViewModel();

        _mainViewModel = new MainViewModel(
            homePage,
            settingsPage,
            logsPage,
            aboutPage);

        var mainWindow = new MainWindow
        {
            DataContext = _mainViewModel
        };

        mainWindow.WindowHidden += () => { SetDockVisibility(false); };

        mainWindow.Closed += (_, _) =>
        {
            homePage.Dispose();
            logsPage.Dispose();
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
            Dispatcher.UIThread.Post(() => { ShowMainWindow(desktop, setupWindow); });
        };

        desktop.MainWindow = setupWindow;

        SetDockVisibility(true);

        setupWindow.Show();
        setupWindow.Activate();

        SetDockIcon();

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

            SetDockIcon();
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

    private void Exit_Click(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private async void OnDesktopExit(
        object? sender,
        ControlledApplicationLifetimeExitEventArgs e)
    {
        try
        {
            // При перезапуске службы уже остановлены в RequestRestart().
            if (!_shutdownPrepared)
            {
                if (_agentRuntime is not null)
                    await _agentRuntime.StopAsync();

                if (_host is not null)
                    await _host.StopAsync();
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"Application shutdown error: {exception}");
        }
        finally
        {
            _host?.Dispose();
            _host = null;

            _instanceMutex?.Dispose();
            _instanceMutex = null;
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

        if (_trayIcon is not null &&
            _agentOnIcon is not null &&
            _agentOffIcon is not null)
        {
            _trayIcon.Icon = isRunning ? _agentOnIcon : _agentOffIcon;
        }
    }

    private async void RequestRestart()
    {
        if (_restartRequested)
            return;

        _restartRequested = true;

        try
        {
            Console.WriteLine("Preparing application restart...");

            if (_agentRuntime is not null)
                await _agentRuntime.StopAsync();

            if (_host is not null)
                await _host.StopAsync();

            _shutdownPrepared = true;

            Console.WriteLine("Application services stopped.");

            StartNewInstance();

            Console.WriteLine("Restart helper launched.");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
        }
        catch (Exception exception)
        {
            _restartRequested = false;

            Console.Error.WriteLine(
                $"Application restart error: {exception}");
        }
    }

    private static void StartNewInstance()
    {
        var processPath = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(processPath))
            throw new InvalidOperationException(
                "Unable to determine application executable path.");

        var arguments = Environment.GetCommandLineArgs();

        var isDotnetHost = string.Equals(
            Path.GetFileNameWithoutExtension(processPath),
            "dotnet",
            StringComparison.OrdinalIgnoreCase);

        if (OperatingSystem.IsMacOS())
        {
            var script = "while /bin/kill -0 \"$1\" 2>/dev/null; do /bin/sleep 0.2; done; shift; exec \"$@\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false
            };

            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(script);
            startInfo.ArgumentList.Add("PCRemoteRestart");
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
            startInfo.ArgumentList.Add(processPath);

            // When the app is started by the dotnet host, GetCommandLineArgs()[0]
            // contains the application assembly, which the host must receive
            // as its first argument. A native apphost already knows its
            // assembly, so its arguments start at index 1.
            if (isDotnetHost)
                startInfo.ArgumentList.Add(arguments[0]);

            for (var i = 1; i < arguments.Length; i++)
            {
                startInfo.ArgumentList.Add(arguments[i]);
            }

            var process = Process.Start(startInfo);

            if (process is null)
                throw new InvalidOperationException(
                    "Failed to start restart helper.");

            Console.WriteLine($"Restart helper PID: {process.Id}");

            return;
        }

        var windowsStartInfo = new ProcessStartInfo
        {
            FileName = processPath,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false
        };

        var firstArgumentIndex = isDotnetHost ? 0 : 1;

        for (var i = firstArgumentIndex; i < arguments.Length; i++)
        {
            windowsStartInfo.ArgumentList.Add(arguments[i]);
        }

        var windowsProcess = Process.Start(windowsStartInfo);

        if (windowsProcess is null)
            throw new InvalidOperationException(
                "Failed to start application process.");
    }
}
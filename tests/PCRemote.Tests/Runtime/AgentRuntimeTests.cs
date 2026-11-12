using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using PCRemote.Agent;
using PCRemote.Agent.Configuration;
using PCRemote.Agent.Runtime;
using PCRemote.Agent.Telegram;
using PCRemote.Core.Interfaces;
using PCRemote.Core.Models;
using PCRemote.Tests.TestDoubles;
using PCRemote.Core.Interfaces.Storage;

namespace PCRemote.Tests.Runtime;

public class AgentRuntimeTests
{
    private static (
        ServiceProvider Provider,
        AgentRuntime Runtime,
        AgentStatus Status,
        TrackingWorker Worker)
        CreateRuntime(
            bool failSystemInfo = false,
            bool failTelegram = false,
            bool failTelegramStartAndStop = false,
            bool failTelegramStop = false,
            bool failWorkerStop = false,
            bool failScopeDispose = false)
    {
        var services = new ServiceCollection();

        var status = new AgentStatus();

        services.AddLogging();
        
        services.AddSingleton<
            ITelegramBotClientFactory,
            TelegramBotClientFactory>();

        services.AddSingleton(status);
        services.AddSingleton<PCRemote.Core.Interfaces.Storage.ISecretStore>(
            new FakeSecretStore());

        services.AddSingleton<ISystemInfoProvider>(
            new FakeSystemInfoProvider(failSystemInfo));
        services.AddSingleton<IScreenshotService, FakeScreenshotService>();
        services.AddSingleton<IPowerManager, FakePowerManager>();

        services.AddSingleton<IOptions<TelegramSettings>>(
            Options.Create(new TelegramSettings()));

        var trackingWorker = new TrackingWorker(
            NullLogger<Worker>.Instance,
            new FakeSystemInfoProvider(),
            failWorkerStop);

        services.AddSingleton<Worker>(trackingWorker);
        
        if (failTelegramStartAndStop)
        {
            services.AddTransient<TelegramBotService, FailingTelegramStartAndStopBotService>();
        }
        else if (failTelegramStop)
        {
            services.AddTransient<TelegramBotService, FailingTelegramStopBotService>();
        }
        else if (failTelegram)
        {
            services.AddTransient<TelegramBotService, FailingTelegramBotService>();
        }
        else
        {
            services.AddTransient<TelegramBotService>();
        }

        var provider = services.BuildServiceProvider();

        IServiceScopeFactory scopeFactory =
            provider.GetRequiredService<IServiceScopeFactory>();

        if (failScopeDispose)
        {
            scopeFactory = new FailingDisposeScopeFactory(scopeFactory);
        }

        var runtime = new AgentRuntime(
            scopeFactory,
            status,
            provider.GetRequiredService<ILogger<AgentRuntime>>());

        return (provider, runtime, status, trackingWorker);
    }

    [Fact]
    public async Task StartAsync_ShouldSetAgentStatusToRunning()
    {
        var setup = CreateRuntime();
        await using var provider = setup.Provider;

        await setup.Runtime.StartAsync();

        Assert.True(setup.Status.IsRunning);

        await setup.Runtime.StopAsync();
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_ShouldRemainRunning()
    {
        var setup = CreateRuntime();
        await using var provider = setup.Provider;

        await setup.Runtime.StartAsync();
        await setup.Runtime.StartAsync();

        Assert.True(setup.Status.IsRunning);

        await setup.Runtime.StopAsync();
    }

    [Fact]
    public async Task StopAsync_ShouldResetAgentStatus()
    {
        var setup = CreateRuntime();
        await using var provider = setup.Provider;

        await setup.Runtime.StartAsync();

        setup.Status.IsTelegramConnected = true;

        await setup.Runtime.StopAsync();

        Assert.False(setup.Status.IsRunning);
        Assert.False(setup.Status.IsTelegramConnected);
    }

    [Fact]
    public async Task StopAsync_WhenAlreadyStopped_ShouldNotThrow()
    {
        var setup = CreateRuntime();
        await using var provider = setup.Provider;

        var exception = await Record.ExceptionAsync(
            () => setup.Runtime.StopAsync());

        Assert.Null(exception);
    }

    private sealed class FakeSystemInfoProvider : ISystemInfoProvider
    {
        private readonly bool _shouldFail;

        public FakeSystemInfoProvider(bool shouldFail = false)
        {
            _shouldFail = shouldFail;
        }

        public Task<SystemInfo> GetSystemInfoAsync(
            CancellationToken cancellationToken = default)
        {
            if (_shouldFail)
            {
                throw new InvalidOperationException(
                    "Simulated system information error.");
            }

            return Task.FromResult(new SystemInfo
            {
                OperatingSystem = "Test OS",
                OSVersion = "1.0",
                Processor = "Test CPU",
                GraphicCard = "Test GPU",
                TotalMemoryBytes = 8L * 1024 * 1024 * 1024,
                FreeDiskSpaceBytes = 100L * 1024 * 1024 * 1024
            });
        }
    }

    private sealed class FakeScreenshotService : IScreenshotService
    {
        public Task<Stream> CaptureAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Stream>(new MemoryStream());
        }
    }

    private sealed class FakePowerManager : IPowerManager
    {
        public Task ShutdownAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RestartAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SleepAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
    
    [Fact]
    public async Task StartAsync_WhenTelegramFails_ShouldResetAgentStatus()
    {
        var setup = CreateRuntime(failTelegram: true);
        await using var provider = setup.Provider;

        setup.Status.IsTelegramConnected = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => setup.Runtime.StartAsync());

        Assert.False(setup.Status.IsRunning);
        Assert.False(setup.Status.IsTelegramConnected);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => setup.Runtime.StartAsync());
    }
    
    private class FailingTelegramBotService : TelegramBotService
    {
        public FailingTelegramBotService(
            ILogger<TelegramBotService> logger,
            PCRemote.Core.Interfaces.Storage.ISecretStore secretStore,
            IOptions<TelegramSettings> options,
            ISystemInfoProvider systemInfoProvider,
            IScreenshotService screenshotService,
            IPowerManager powerManager,
            AgentStatus agentStatus,
            ITelegramBotClientFactory botClientFactory)
            : base(
                logger,
                secretStore,
                options,
                systemInfoProvider,
                screenshotService,
                powerManager,
                agentStatus,
                botClientFactory)
        {
        }

        public override Task StartAsync(
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Simulated Telegram startup error.");
        }
    }
    
    private sealed class FailingTelegramStopBotService : TelegramBotService
    {
        public FailingTelegramStopBotService(
            ILogger<TelegramBotService> logger,
            PCRemote.Core.Interfaces.Storage.ISecretStore secretStore,
            IOptions<TelegramSettings> options,
            ISystemInfoProvider systemInfoProvider,
            IScreenshotService screenshotService,
            IPowerManager powerManager,
            AgentStatus agentStatus,
            ITelegramBotClientFactory botClientFactory)
            : base(
                logger,
                secretStore,
                options,
                systemInfoProvider,
                screenshotService,
                powerManager,
                agentStatus,
                botClientFactory)
        {
        }

        public override Task StopAsync(
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Simulated Telegram stop error.");
        }
    }
    
    private sealed class TrackingWorker : Worker
    {
        private readonly bool _failOnStop;

        public bool StopCalled { get; private set; }

        public TrackingWorker(
            ILogger<Worker> logger,
            ISystemInfoProvider systemInfoProvider,
            bool failOnStop = false)
            : base(logger, systemInfoProvider)
        {
            _failOnStop = failOnStop;
        }

        public override Task StopAsync(
            CancellationToken cancellationToken = default)
        {
            StopCalled = true;

            if (_failOnStop)
            {
                throw new InvalidOperationException(
                    "Simulated Worker stop error.");
            }

            return base.StopAsync(cancellationToken);
        }
    }
    
    private sealed class FailingDisposeScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceScopeFactory _inner;

        public FailingDisposeScopeFactory(IServiceScopeFactory inner)
        {
            _inner = inner;
        }

        public IServiceScope CreateScope()
        {
            return new FailingDisposeScope(_inner.CreateScope());
        }
    }

    private sealed class FailingDisposeScope : IServiceScope, IAsyncDisposable
    {
        private readonly IServiceScope _inner;

        public FailingDisposeScope(IServiceScope inner)
        {
            _inner = inner;
        }

        public IServiceProvider ServiceProvider => _inner.ServiceProvider;

        public void Dispose()
        {
            _inner.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (_inner is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                _inner.Dispose();
            }

            throw new InvalidOperationException(
                "Simulated scope disposal error.");
        }
    }
    
    private sealed class FailingTelegramStartAndStopBotService : TelegramBotService
    {
        public FailingTelegramStartAndStopBotService(
            ILogger<TelegramBotService> logger,
            PCRemote.Core.Interfaces.Storage.ISecretStore secretStore,
            IOptions<TelegramSettings> options,
            ISystemInfoProvider systemInfoProvider,
            IScreenshotService screenshotService,
            IPowerManager powerManager,
            AgentStatus agentStatus,
            ITelegramBotClientFactory botClientFactory)
            : base(
                logger,
                secretStore,
                options,
                systemInfoProvider,
                screenshotService,
                powerManager,
                agentStatus,
                botClientFactory)
        {
        }

        public override Task StartAsync(
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Simulated Telegram startup error.");
        }

        public override Task StopAsync(
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Simulated Telegram stop error.");
        }
    }
    
    [Fact]
    public async Task StopAsync_WhenTelegramStopFails_ShouldStillStopWorker()
    {
        var setup = CreateRuntime(failTelegramStop: true);
        await using var provider = setup.Provider;

        await setup.Runtime.StartAsync();

        var exception = await Record.ExceptionAsync(
            () => setup.Runtime.StopAsync());

        Assert.NotNull(exception);
        Assert.True(setup.Worker.StopCalled);
        Assert.False(setup.Status.IsRunning);
    }
    
    [Fact]
    public async Task StopAsync_WhenTelegramAndWorkerFail_ShouldStillResetStatus()
    {
        var setup = CreateRuntime(
            failTelegramStop: true,
            failWorkerStop: true);

        await using var provider = setup.Provider;

        await setup.Runtime.StartAsync();

        var exception = await Record.ExceptionAsync(
            () => setup.Runtime.StopAsync());

        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);

        Assert.True(setup.Worker.StopCalled);
        Assert.False(setup.Status.IsRunning);
        Assert.False(setup.Status.IsTelegramConnected);
    }
    
    [Fact]
    public async Task StopAsync_WhenScopeDisposalFails_ShouldResetStatus()
    {
        var setup = CreateRuntime(failScopeDispose: true);
        await using var provider = setup.Provider;

        await setup.Runtime.StartAsync();

        var exception = await Record.ExceptionAsync(
            () => setup.Runtime.StopAsync());

        Assert.NotNull(exception);
        Assert.Equal(
            "Simulated scope disposal error.",
            exception.Message);

        Assert.False(setup.Status.IsRunning);
        Assert.False(setup.Status.IsTelegramConnected);

        var secondException = await Record.ExceptionAsync(
            () => setup.Runtime.StopAsync());

        Assert.Null(secondException);
    }
    
    [Fact]
    public async Task StartAsync_WhenStartAndCleanupFail_ShouldPreserveStartupException()
    {
        var setup = CreateRuntime(failTelegramStartAndStop: true);
        await using var provider = setup.Provider;

        var exception = await Record.ExceptionAsync(
            () => setup.Runtime.StartAsync());

        Assert.NotNull(exception);
        Assert.Equal(
            "Simulated Telegram startup error.",
            exception.Message);

        Assert.True(setup.Worker.StopCalled);
        Assert.False(setup.Status.IsRunning);
        Assert.False(setup.Status.IsTelegramConnected);
    }
}

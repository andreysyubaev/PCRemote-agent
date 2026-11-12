using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PCRemote.Agent.Telegram;
using PCRemote.Core.Models;

namespace PCRemote.Agent.Runtime;

public class AgentRuntime
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AgentStatus _agentStatus;
    private readonly ILogger<AgentRuntime> _logger;

    private AsyncServiceScope? _scope;
    private Worker? _worker;
    private TelegramBotService? _telegramBotService;

    public AgentRuntime(
        IServiceScopeFactory scopeFactory,
        AgentStatus agentStatus,
        ILogger<AgentRuntime> logger)
    {
        _scopeFactory = scopeFactory;
        _agentStatus = agentStatus;
        _logger = logger;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        if (_scope is not null)
        {
            _logger.LogDebug("Agent is already running.");
            return;
        }

        _logger.LogInformation("Starting agent...");

        _scope = _scopeFactory.CreateAsyncScope();

        try
        {
            _worker = _scope.Value.ServiceProvider
                .GetRequiredService<Worker>();

            _telegramBotService = _scope.Value.ServiceProvider
                .GetRequiredService<TelegramBotService>();

            await _worker.StartAsync(cancellationToken);
            await _telegramBotService.StartAsync(cancellationToken);

            _agentStatus.IsRunning = true;

            _logger.LogInformation("Agent started successfully.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to start agent.");

            try
            {
                await StopAsync(CancellationToken.None);
            }
            catch (Exception stopException)
            {
                _logger.LogError(
                    stopException,
                    "Failed to clean up after agent startup failure.");
            }

            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_scope is null)
        {
            _logger.LogDebug("Agent is already stopped.");
            return;
        }

        _logger.LogInformation("Stopping agent...");

        Exception? stopException = null;

        try
        {
            if (_telegramBotService is not null)
            {
                try
                {
                    await _telegramBotService.StopAsync(cancellationToken);
                }
                catch (Exception exception)
                {
                    stopException = exception;
                    _logger.LogError(exception, "Error while stopping Telegram bot.");
                }
            }

            if (_worker is not null)
            {
                try
                {
                    await _worker.StopAsync(cancellationToken);
                }
                catch (Exception exception)
                {
                    stopException ??= exception;
                    _logger.LogError(exception, "Error while stopping worker.");
                }
            }
        }
        finally
        {
            try
            {
                await _scope.Value.DisposeAsync();
            }
            finally
            {
                _scope = null;
                _worker = null;
                _telegramBotService = null;

                _agentStatus.IsRunning = false;
                _agentStatus.IsTelegramConnected = false;

                _logger.LogInformation("Agent stopped.");
            }
        }

        if (stopException is not null)
            throw stopException;
    }
}

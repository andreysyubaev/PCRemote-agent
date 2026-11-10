using Microsoft.Extensions.DependencyInjection;
using PCRemote.Agent.Telegram;
using PCRemote.Core.Models;
namespace PCRemote.Agent.Runtime;

public class AgentRuntime
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AgentStatus _agentStatus;

    private AsyncServiceScope? _scope;
    private Worker? _worker;
    private TelegramBotService? _telegramBotService;

    public AgentRuntime(
        IServiceScopeFactory scopeFactory,
        AgentStatus agentStatus)
    {
        _scopeFactory = scopeFactory;
        _agentStatus = agentStatus;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        if (_scope is not null)
            return;

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
        }
        catch
        {
            await StopAsync(cancellationToken);
            throw;
        }
    }

    public async Task StopAsync(
        CancellationToken cancellationToken = default)
    {
        if (_scope is null)
            return;

        try
        {
            if (_telegramBotService is not null)
                await _telegramBotService.StopAsync(cancellationToken);

            if (_worker is not null)
                await _worker.StopAsync(cancellationToken);
        }
        finally
        {
            await _scope.Value.DisposeAsync();

            _scope = null;
            _worker = null;
            _telegramBotService = null;

            _agentStatus.IsRunning = false;
            _agentStatus.IsTelegramConnected = false;
        }
    }
}
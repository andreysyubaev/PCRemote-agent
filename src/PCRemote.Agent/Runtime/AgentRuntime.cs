using PCRemote.Agent.Telegram;
using PCRemote.Core.Models;

namespace PCRemote.Agent.Runtime;

public class AgentRuntime
{
    private readonly Worker _worker;
    private readonly TelegramBotService _telegramBotService;
    private readonly AgentStatus _agentStatus;

    public AgentRuntime(
        Worker worker,
        TelegramBotService telegramBotService,
        AgentStatus agentStatus)
    {
        _worker = worker;
        _telegramBotService = telegramBotService;
        _agentStatus = agentStatus;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        await _worker.StartAsync(cancellationToken);
        await _telegramBotService.StartAsync(cancellationToken);
        
        _agentStatus.IsRunning = true;
    }

    public async Task StopAsync(
        CancellationToken cancellationToken = default)
    {
        await _telegramBotService.StopAsync(cancellationToken);
        await _worker.StopAsync(cancellationToken);
        
        _agentStatus.IsRunning = false;
        _agentStatus.IsTelegramConnected = false;
    }
}
using PCRemote.Agent.Telegram;

namespace PCRemote.Agent.Runtime;

public class AgentRuntime
{
    private readonly Worker _worker;
    private readonly TelegramBotService _telegramBotService;

    public AgentRuntime(
        Worker worker,
        TelegramBotService telegramBotService)
    {
        _worker = worker;
        _telegramBotService = telegramBotService;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        await _worker.StartAsync(cancellationToken);
        await _telegramBotService.StartAsync(cancellationToken);
    }

    public async Task StopAsync(
        CancellationToken cancellationToken = default)
    {
        await _telegramBotService.StopAsync(cancellationToken);
        await _worker.StopAsync(cancellationToken);
    }
}
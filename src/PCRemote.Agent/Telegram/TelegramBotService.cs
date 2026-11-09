using Microsoft.Extensions.Options;
using PCRemote.Agent.Configuration;
using Telegram.Bot;

namespace PCRemote.Agent.Telegram;

public class TelegramBotService : BackgroundService
{
    private readonly ILogger<TelegramBotService> _logger;
    private readonly TelegramBotClient _botClient;
    private readonly TelegramSettings _settings;

    public TelegramBotService(
        ILogger<TelegramBotService> logger,
        IOptions<TelegramSettings> options)
    {
        _logger = logger;
        _settings = options.Value;

        _botClient = new TelegramBotClient(_settings.BotToken);
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            var botInfo = await _botClient.GetMe(
                cancellationToken: stoppingToken);

            _logger.LogInformation(
                "Telegram bot connected: @{Username}",
                botInfo.Username);

            int offset = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                var updates = await _botClient.GetUpdates(
                    offset: offset,
                    timeout: 30,
                    cancellationToken: stoppingToken);

                foreach (var update in updates)
                {
                    offset = update.Id + 1;

                    var message = update.Message;

                    if (message?.Text is null)
                    {
                        continue;
                    }

                    if (message.From?.Id != _settings.AllowedUserId)
                    {
                        _logger.LogWarning(
                            "Unauthorized Telegram user attempted access: {UserId}",
                            message.From?.Id);

                        continue;
                    }

                    _logger.LogInformation(
                        "Received Telegram message: {Message}",
                        message.Text);

                    if (message.Text == "/start")
                    {
                        await _botClient.SendMessage(
                            chatId: message.Chat.Id,
                            text: "PCRemote Agent успешно подключён! Бот готов к работе.",
                            cancellationToken: stoppingToken);
                    }
                    else
                    {
                        await _botClient.SendMessage(
                            chatId: message.Chat.Id,
                            text: "Пока доступна только команда /start. Остальные команды добавим позже.",
                            cancellationToken: stoppingToken);
                    }
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Telegram bot service is stopping.");
        }
    }
}
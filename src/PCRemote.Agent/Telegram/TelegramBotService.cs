
using Microsoft.Extensions.Options;
using PCRemote.Agent.Configuration;
using PCRemote.Core.Interfaces;
using Telegram.Bot;

namespace PCRemote.Agent.Telegram;

public class TelegramBotService : BackgroundService
{
    private readonly ILogger<TelegramBotService> _logger;
    private readonly TelegramBotClient _botClient;
    private readonly TelegramSettings _settings;
    private readonly ISystemInfoProvider _systemInfoProvider;

    public TelegramBotService(
        ILogger<TelegramBotService> logger,
        IOptions<TelegramSettings> options,
        ISystemInfoProvider systemInfoProvider)
    {
        _logger = logger;
        _settings = options.Value;
        _systemInfoProvider = systemInfoProvider;

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

                    var command = message.Text.Trim();

                    switch (command)
                    {
                        case "/start":
                            await _botClient.SendMessage(
                                chatId: message.Chat.Id,
                                text: "PCRemote Agent успешно подключён! Бот готов к работе.",
                                cancellationToken: stoppingToken);
                            break;

                        case "/status":
                            await SendSystemInfoAsync(
                                message.Chat.Id,
                                stoppingToken);
                            break;

                        default:
                            await _botClient.SendMessage(
                                chatId: message.Chat.Id,
                                text: "Неизвестная команда. Доступные команды: /start, /status",
                                cancellationToken: stoppingToken);
                            break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Telegram bot service is stopping.");
        }
    }

    private async Task SendSystemInfoAsync(
        long chatId,
        CancellationToken cancellationToken)
    {
        try
        {
            var systemInfo = await _systemInfoProvider.GetSystemInfoAsync(
                cancellationToken);

            var totalMemoryGb =
                systemInfo.TotalMemoryBytes / 1024d / 1024 / 1024;

            var freeDiskGb =
                systemInfo.FreeDiskSpaceBytes / 1024d / 1024 / 1024;

            var text = $"""
                        💻 Информация о компьютере

                        🖥 Операционная система: {systemInfo.OperatingSystem}
                        📌 Версия: {systemInfo.OSVersion}
                        ⚙️ Процессор: {systemInfo.Processor}
                        🎮 Видеокарта: {systemInfo.GraphicCard}

                        🧠 Оперативная память: {totalMemoryGb:F1} GB
                        💾 Свободное место: {freeDiskGb:F1} GB
                        """;

            await _botClient.SendMessage(
                chatId: chatId,
                text: text,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "System information sent to Telegram.");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to retrieve system information.");

            await _botClient.SendMessage(
                chatId: chatId,
                text: "Не удалось получить информацию о системе.",
                cancellationToken: cancellationToken);
        }
    }
}

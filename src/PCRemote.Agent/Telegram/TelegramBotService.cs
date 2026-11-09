
using Microsoft.Extensions.Options;
using PCRemote.Agent.Configuration;
using PCRemote.Core.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramCallbackQuery = Telegram.Bot.Types.CallbackQuery;

namespace PCRemote.Agent.Telegram;

public class TelegramBotService : BackgroundService
{
    private readonly ILogger<TelegramBotService> _logger;
    private readonly TelegramBotClient _botClient;
    private readonly TelegramSettings _settings;
    private readonly ISystemInfoProvider _systemInfoProvider;
    private readonly IScreenshotService _screenshotService;
    private readonly IPowerManager _powerManager;

    public TelegramBotService(
        ILogger<TelegramBotService> logger,
        IOptions<TelegramSettings> options,
        ISystemInfoProvider systemInfoProvider,
        IScreenshotService screenshotService,
        IPowerManager powerManager)
    {
        _logger = logger;
        _settings = options.Value;
        _systemInfoProvider = systemInfoProvider;
        _screenshotService = screenshotService;
        _powerManager = powerManager;

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
                    
                    if (update.CallbackQuery is not null)
                    {
                        await HandleCallbackQueryAsync(
                            update.CallbackQuery,
                            stoppingToken);

                        continue;
                    }

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
                        
                        case "/screenshot":
                            await SendScreenshotAsync(
                                message.Chat.Id,
                                stoppingToken);
                            break;
                        
                        case "/shutdown":
                            await SendPowerConfirmationAsync(
                                message.Chat.Id,
                                "shutdown",
                                "выключить компьютер",
                                stoppingToken);
                            break;

                        case "/restart":
                            await SendPowerConfirmationAsync(
                                message.Chat.Id,
                                "restart",
                                "перезагрузить компьютер",
                                stoppingToken);
                            break;

                        case "/sleep":
                            await SendPowerConfirmationAsync(
                                message.Chat.Id,
                                "sleep",
                                "перевести компьютер в спящий режим",
                                stoppingToken);
                            break;

                        default:
                            await _botClient.SendMessage(
                                chatId: message.Chat.Id,
                                text: "Неизвестная команда. Доступные команды: /start, /status, /screenshot, /shutdown, /restart, /sleep",
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
    
    private async Task SendScreenshotAsync(
        long chatId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var screenshot = await _screenshotService.CaptureAsync(
                cancellationToken);

            await _botClient.SendPhoto(
                chatId: chatId,
                photo: InputFile.FromStream(screenshot, "screenshot.png"),
                caption: "📸 Скриншот твоего компьютера",
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Screenshot sent to Telegram.");
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
                "Failed to capture or send screenshot.");

            await _botClient.SendMessage(
                chatId: chatId,
                text: "Не удалось сделать или отправить скриншот.",
                cancellationToken: cancellationToken);
        }
    }   
    
    private async Task SendPowerConfirmationAsync(
        long chatId,
        string action,
        string description,
        CancellationToken cancellationToken)
    {
        var keyboard = new InlineKeyboardMarkup(
            new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "✅ Подтвердить",
                        $"power_confirm:{action}"),

                    InlineKeyboardButton.WithCallbackData(
                        "❌ Отмена",
                        "power_cancel")
                }
            });

        await _botClient.SendMessage(
            chatId: chatId,
            text: $"⚠️ Ты действительно хочешь {description}?",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }
    
    private async Task HandleCallbackQueryAsync(
        TelegramCallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        if (callbackQuery.From.Id != _settings.AllowedUserId)
        {
            await _botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: "У тебя нет доступа.",
                showAlert: true,
                cancellationToken: cancellationToken);

            return;
        }

        if (callbackQuery.Message is null)
        {
            return;
        }

        var chatId = callbackQuery.Message.Chat.Id;
        var callbackData = callbackQuery.Data;

        if (callbackData == "power_cancel")
        {
            await _botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: "Действие отменено.",
                cancellationToken: cancellationToken);

            await _botClient.SendMessage(
                chatId: chatId,
                text: "❌ Действие отменено.",
                cancellationToken: cancellationToken);

            return;
        }

        const string prefix = "power_confirm:";

        if (callbackData is null ||
            !callbackData.StartsWith(prefix))
        {
            await _botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: "Неизвестное действие.",
                cancellationToken: cancellationToken);

            return;
        }

        var action = callbackData[prefix.Length..];

        await _botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            text: "Подтверждение получено.",
            cancellationToken: cancellationToken);

        await ExecutePowerActionAsync(
            chatId,
            action,
            cancellationToken);
    }
    
    private async Task ExecutePowerActionAsync(
        long chatId,
        string action,
        CancellationToken cancellationToken)
    {
        await _botClient.SendMessage(
            chatId: chatId,
            text: "⏳ Выполняю команду...",
            cancellationToken: cancellationToken);

        switch (action)
        {
            case "shutdown":
                await _powerManager.ShutdownAsync(cancellationToken);
                break;

            case "restart":
                await _powerManager.RestartAsync(cancellationToken);
                break;

            case "sleep":
                await _powerManager.SleepAsync(cancellationToken);
                break;

            default:
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "Неизвестное действие.",
                    cancellationToken: cancellationToken);
                break;
        }
    }
}

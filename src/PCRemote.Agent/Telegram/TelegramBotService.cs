using PCRemote.Core.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramCallbackQuery = Telegram.Bot.Types.CallbackQuery;
using Telegram.Bot.Exceptions;
using System.Collections.Concurrent;
using PCRemote.Agent.Configuration;
using PCRemote.Core.Interfaces.Storage;
using Microsoft.Extensions.Options;

namespace PCRemote.Agent.Telegram;

public class TelegramBotService : BackgroundService
{
    private readonly ILogger<TelegramBotService> _logger;
    private readonly ISecretStore _secretStore;
    private TelegramBotClient _botClient = null!;
    private readonly ISystemInfoProvider _systemInfoProvider;
    private readonly IScreenshotService _screenshotService;
    private readonly IPowerManager _powerManager;
    private readonly ConcurrentDictionary<string, PendingPowerAction>
        _pendingActions = new();
    private readonly TelegramSettings _settings;

    public TelegramBotService(
        ILogger<TelegramBotService> logger,
        ISecretStore secretStore,
        IOptions<TelegramSettings> options,
        ISystemInfoProvider systemInfoProvider,
        IScreenshotService screenshotService,
        IPowerManager powerManager)
    {
        _logger = logger;
        _secretStore = secretStore;
        _settings = options.Value;
        _systemInfoProvider = systemInfoProvider;
        _screenshotService = screenshotService;
        _powerManager = powerManager;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        int offset = 0;
        bool isConnected = false;
        
        var botToken = await _secretStore.GetSecretAsync(
            SecretKeys.TelegramBotToken,
            stoppingToken);

        if (string.IsNullOrWhiteSpace(botToken))
        {
            _logger.LogError(
                "Telegram bot token was not found in secure storage.");

            return;
        }

        _botClient = new TelegramBotClient(botToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!isConnected)
                {
                    var botInfo = await _botClient.GetMe(
                        cancellationToken: stoppingToken);

                    _logger.LogInformation(
                        "Telegram bot connected: @{Username}",
                        botInfo.Username);

                    isConnected = true;
                }

                var updates = await _botClient.GetUpdates(
                    offset: offset,
                    timeout: 30,
                    cancellationToken: stoppingToken);

                foreach (var update in updates)
                {
                    if (update.CallbackQuery is not null)
                    {
                        await HandleCallbackQueryAsync(
                            update.CallbackQuery,
                            stoppingToken);

                        offset = update.Id + 1;
                        continue;
                    }

                    var message = update.Message;

                    if (message?.Text is null)
                    {
                        offset = update.Id + 1;
                        continue;
                    }

                    if (message.From?.Id != _settings.AllowedUserId)
                    {
                        _logger.LogWarning(
                            "Unauthorized Telegram user attempted access: {UserId}",
                            message.From?.Id);

                        offset = update.Id + 1;
                        continue;
                    }

                    var command = message.Text.Trim();

                    switch (command)
                    {
                        case "/start":
                            await _botClient.SendMessage(
                                chatId: message.Chat.Id,
                                text: "PCRemote Agent успешно подключён! Выбери действие:",
                                replyMarkup: CreateMainMenu(),
                                cancellationToken: stoppingToken);
                            break;

                        case "/status":
                        case "📊 Статус ПК":
                            await SendSystemInfoAsync(message.Chat.Id, stoppingToken);
                            break;

                        case "/screenshot":
                        case "📸 Скриншот":
                            await SendScreenshotAsync(message.Chat.Id, stoppingToken);
                            break;

                        case "/shutdown":
                        case "🔴 Выключение":
                            await SendPowerConfirmationAsync(
                                message.Chat.Id,
                                "shutdown",
                                "выключить компьютер",
                                stoppingToken);
                            break;

                        case "/restart":
                        case "🔄 Перезагрузка":
                            await SendPowerConfirmationAsync(
                                message.Chat.Id,
                                "restart",
                                "перезагрузить компьютер",
                                stoppingToken);
                            break;

                        case "/sleep":
                        case "💤 Сон":
                            await SendPowerConfirmationAsync(
                                message.Chat.Id,
                                "sleep",
                                "перевести компьютер в спящий режим",
                                stoppingToken);
                            break;
                        
                        case "ℹ️ Помощь":
                        case "/help":
                        {
                            var helpKeyboard = new InlineKeyboardMarkup(
                                new[]
                                {
                                    new[]
                                    {
                                        InlineKeyboardButton.WithUrl(
                                            "⭐ GitHub — исходный код",
                                            "https://github.com/andreysyubaev/PCRemote-agent")
                                    }
                                });

                            await _botClient.SendMessage(
                                chatId: message.Chat.Id,
                                text:
                                "🖥 PCRemote\n\n" +
                                "Управляй своим компьютером прямо из Telegram!\n\n" +
                                "📌 Возможности:\n" +
                                "• 📊 Получение информации о системе\n" +
                                "• 📸 Создание скриншотов рабочего стола\n" +
                                "• 💤 Перевод компьютера в спящий режим\n" +
                                "• 🔄 Перезагрузка компьютера\n" +
                                "• 🔴 Удалённое выключение\n\n" +
                                "💻 Поддерживаемые системы: Windows и macOS.\n\n" +
                                "🔐 Безопасность:\n" +
                                "Доступ разрешён только авторизованному пользователю. " +
                                "Для опасных действий предусмотрено подтверждение.\n\n" +
                                "🚀 PCRemote — Open Source проект.\n" +
                                "Исходный код и развитие проекта доступны на GitHub:",
                                replyMarkup: helpKeyboard,
                                cancellationToken: stoppingToken);

                            break;
                        }

                        default:
                            await _botClient.SendMessage(
                                chatId: message.Chat.Id,
                                text: "Неизвестная команда. Используй кнопки меню ниже.",
                                replyMarkup: CreateMainMenu(),
                                cancellationToken: stoppingToken);
                            break;
                    }

                    offset = update.Id + 1;
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Telegram bot service is stopping.");

                break;
            }
            catch (RequestException exception)
                when (exception.InnerException is HttpRequestException)
            {
                isConnected = false;

                _logger.LogWarning(
                    exception,
                    "Telegram connection lost. Retrying in 5 seconds...");
            }
            catch (ApiRequestException exception)
                when (exception.ErrorCode == 429 || exception.ErrorCode >= 500)
            {
                isConnected = false;

                _logger.LogWarning(
                    exception,
                    "Telegram API temporarily unavailable. Retrying in 5 seconds...");
            }

            if (!stoppingToken.IsCancellationRequested && !isConnected)
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    stoppingToken);
            }
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
        var confirmationId = Guid.NewGuid().ToString("N");
        
        // Удаляем предыдущие неподтверждённые действия в этом чате.
        foreach (var entry in _pendingActions.ToArray())
        {
            if (entry.Value.ChatId != chatId)
                continue;

            if (!_pendingActions.TryRemove(entry.Key, out var previousAction))
                continue;

            try
            {
                await _botClient.DeleteMessage(
                    chatId: chatId,
                    messageId: previousAction.MessageId,
                    cancellationToken: cancellationToken);
            }
            catch (ApiRequestException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to delete previous confirmation message: {MessageId}",
                    previousAction.MessageId);
            }
        }

        var keyboard = new InlineKeyboardMarkup(
            new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "✅ Подтвердить",
                        $"power_confirm:{confirmationId}"),
                    InlineKeyboardButton.WithCallbackData(
                        "❌ Отмена",
                        $"power_cancel:{confirmationId}")
                }
            });

        var sentMessage = await _botClient.SendMessage(
            chatId: chatId,
            text: $"⚠️ Ты действительно хочешь {description}?\n\nПодтверждение действительно 2 минуты.",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);

        _pendingActions[confirmationId] = new PendingPowerAction(
            action,
            chatId,
            sentMessage.MessageId,
            DateTimeOffset.UtcNow.AddMinutes(2));
    }
    
    private async Task HandleCallbackQueryAsync(
    TelegramCallbackQuery callbackQuery,
    CancellationToken cancellationToken)
{
    // Проверяем, что кнопку нажал разрешённый пользователь.
    if (callbackQuery.From.Id != _settings.AllowedUserId)
    {
        await _botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            text: "У тебя нет доступа.",
            showAlert: true,
            cancellationToken: cancellationToken);

        return;
    }

    if (callbackQuery.Message is null ||
        string.IsNullOrWhiteSpace(callbackQuery.Data))
    {
        return;
    }

    const string confirmPrefix = "power_confirm:";
    const string cancelPrefix = "power_cancel:";

    var callbackData = callbackQuery.Data;
    var isConfirmation = callbackData.StartsWith(confirmPrefix);
    var isCancellation = callbackData.StartsWith(cancelPrefix);

    if (!isConfirmation && !isCancellation)
    {
        await _botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            text: "Неизвестное действие.",
            showAlert: true,
            cancellationToken: cancellationToken);

        return;
    }

    var prefix = isConfirmation ? confirmPrefix : cancelPrefix;
    var confirmationId = callbackData[prefix.Length..];

    if (!_pendingActions.TryGetValue(
            confirmationId, out var pendingAction))
    {
        await _botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            text: "Это подтверждение уже недействительно.",
            showAlert: true,
            cancellationToken: cancellationToken);

        return;
    }

    var chatId = callbackQuery.Message.Chat.Id;

    if (pendingAction.ChatId != chatId ||
        pendingAction.MessageId != callbackQuery.Message.MessageId)
    {
        await _botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            text: "Подтверждение не найдено.",
            showAlert: true,
            cancellationToken: cancellationToken);

        return;
    }

    if (pendingAction.ExpiresAt <= DateTimeOffset.UtcNow)
    {
        _pendingActions.TryRemove(confirmationId, out _);

        await _botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            text: "Время подтверждения истекло.",
            showAlert: true,
            cancellationToken: cancellationToken);

        await _botClient.EditMessageText(
            chatId: chatId,
            messageId: pendingAction.MessageId,
            text: "⌛ Время подтверждения истекло.",
            cancellationToken: cancellationToken);

        return;
    }

    // Удаляем запись, чтобы кнопку нельзя было использовать повторно.
    if (!_pendingActions.TryRemove(
            confirmationId, out var confirmedAction))
    {
        await _botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            text: "Это действие уже обработано.",
            showAlert: true,
            cancellationToken: cancellationToken);

        return;
    }

    await _botClient.AnswerCallbackQuery(
        callbackQueryId: callbackQuery.Id,
        cancellationToken: cancellationToken);

    if (isCancellation)
    {
        await _botClient.DeleteMessage(
            chatId: chatId,
            messageId: confirmedAction.MessageId,
            cancellationToken: cancellationToken);

        return;
    }

    await _botClient.EditMessageText(
        chatId: chatId,
        messageId: confirmedAction.MessageId,
        text: "⏳ Команда принята, выполняю...",
        cancellationToken: cancellationToken);

    await ExecutePowerActionAsync(
        chatId,
        confirmedAction.MessageId,
        confirmedAction.Action,
        cancellationToken);
    }
    
    private async Task ExecutePowerActionAsync(
        long chatId,
        int messageId,
        string action,
        CancellationToken cancellationToken)
    {
        try
        {
            string resultText;

            switch (action)
            {
                case "shutdown":
                    await _powerManager.ShutdownAsync(cancellationToken);
                    resultText = "🖥 Компьютер выключен.";
                    break;

                case "restart":
                    await _powerManager.RestartAsync(cancellationToken);
                    resultText = "🔄 Компьютер перезагружен.";
                    break;

                case "sleep":
                    await _powerManager.SleepAsync(cancellationToken);
                    resultText = "💤 Компьютер переведён в спящий режим.";
                    break;

                default:
                    resultText = "❌ Неизвестная команда.";
                    break;
            }

            await _botClient.EditMessageText(
                chatId: chatId,
                messageId: messageId,
                text: resultText,
                cancellationToken: cancellationToken);
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
                "Failed to execute power action: {Action}",
                action);

            await _botClient.EditMessageText(
                chatId: chatId,
                messageId: messageId,
                text: "❌ Не удалось выполнить действие. Проверь состояние компьютера.",
                cancellationToken: cancellationToken);
        }
    }
    
    private record PendingPowerAction(
        string Action,
        long ChatId,
        int MessageId,
        DateTimeOffset ExpiresAt);
    
    private static ReplyKeyboardMarkup CreateMainMenu()
    {
        return new ReplyKeyboardMarkup(
            new[]
            {
                new KeyboardButton[]
                {
                    new("📊 Статус ПК"),
                    new("📸 Скриншот")
                },
                new KeyboardButton[]
                {
                    new("💤 Сон"),
                    new("🔄 Перезагрузка")
                },
                new KeyboardButton[]
                {
                    new("🔴 Выключение"),
                    new("ℹ️ Помощь")
                }
            })
        {
            ResizeKeyboard = true,
            IsPersistent = true,
            InputFieldPlaceholder = "Выбери действие..."
        };
    }
}

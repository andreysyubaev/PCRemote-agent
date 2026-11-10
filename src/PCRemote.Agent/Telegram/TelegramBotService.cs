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
using System.Security.Cryptography;
using PCRemote.Core.Models;

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
    private long? _allowedUserId;
    private string? _pairingCode;
    private DateTimeOffset _pairingCodeExpiresAt;
    private static readonly TimeSpan PairingCodeLifetime =
        TimeSpan.FromMinutes(5);
    private readonly AgentStatus _agentStatus;

    public TelegramBotService(
        ILogger<TelegramBotService> logger,
        ISecretStore secretStore,
        IOptions<TelegramSettings> options,
        ISystemInfoProvider systemInfoProvider,
        IScreenshotService screenshotService,
        IPowerManager powerManager,
        AgentStatus agentStatus)
    {
        _logger = logger;
        _secretStore = secretStore;
        _settings = options.Value;
        _systemInfoProvider = systemInfoProvider;
        _screenshotService = screenshotService;
        _powerManager = powerManager;
        _agentStatus = agentStatus;
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
        
        var storedUserId = await _secretStore.GetSecretAsync(
            SecretKeys.TelegramAllowedUserId,
            stoppingToken);

        if (long.TryParse(storedUserId, out var savedUserId) &&
            savedUserId > 0)
        {
            _allowedUserId = savedUserId;
        }
        else
        {
            _pairingCode = RandomNumberGenerator
                .GetInt32(100000, 1000000)
                .ToString();

            _pairingCodeExpiresAt = DateTimeOffset.UtcNow
                .Add(PairingCodeLifetime);
            
            _agentStatus.PairingCode = _pairingCode;
            _agentStatus.PairingCodeExpiresAt = _pairingCodeExpiresAt;
            _agentStatus.IsRunning = true;

            Console.WriteLine();
            Console.WriteLine("=== PCRemote Pairing ===");
            Console.WriteLine($"Your pairing code: {_pairingCode}");
            Console.WriteLine("Send /pair CODE to your Telegram bot.");
            Console.WriteLine("The code expires in 5 minutes.");
            Console.WriteLine();
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!isConnected)
                {
                    var botInfo = await _botClient.GetMe(cancellationToken: stoppingToken);

                    _logger.LogInformation(
                        "Telegram bot connected: @{Username}",
                        botInfo.Username);
                    
                    _agentStatus.BotUsername = botInfo.Username;
                    _agentStatus.IsTelegramConnected = true;

                    await RegisterBotCommandsAsync(stoppingToken);

                    _logger.LogInformation("Telegram bot commands registered.");

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

                    var command = message.Text.Trim();

                    if (_allowedUserId is null)
                    {
                        if (command.StartsWith("/pair ", StringComparison.OrdinalIgnoreCase))
                        {
                            await HandlePairCommandAsync(message, command, stoppingToken);
                        }
                        else
                        {
                            await _botClient.SendMessage(
                                chatId: message.Chat.Id,
                                text: "🔐 PCRemote ещё не привязан.\n\nОтправь /pair КОД для подключения.",
                                replyMarkup: new ReplyKeyboardRemove(),
                                cancellationToken: stoppingToken);
                            
                        }

                        offset = update.Id + 1;
                        continue;
                    }

                    if (message.From?.Id != _allowedUserId)
                    {
                        _logger.LogWarning(
                            "Unauthorized Telegram user attempted access: {UserId}",
                            message.From?.Id);

                        offset = update.Id + 1;
                        continue;
                    }

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
                _agentStatus.IsTelegramConnected = false;

                _logger.LogWarning(
                    exception,
                    "Telegram connection lost. Retrying in 5 seconds...");
            }
            catch (ApiRequestException exception)
                when (exception.ErrorCode == 429 || exception.ErrorCode >= 500)
            {
                isConnected = false;
                _agentStatus.IsTelegramConnected = false;

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
    if (_allowedUserId is null ||
        callbackQuery.From.Id != _allowedUserId.Value)
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
    
    private async Task HandlePairCommandAsync(
        Message message,
        string command,
        CancellationToken cancellationToken)
    {
        if (_pairingCode is null ||
            DateTimeOffset.UtcNow > _pairingCodeExpiresAt)
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "🔐 PCRemote ещё не привязан.\n\nОтправь /pair КОД для подключения.",
                replyMarkup: new ReplyKeyboardRemove());

            return;
        }

        var parts = command.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2 ||
            !string.Equals(parts[1], _pairingCode, StringComparison.Ordinal))
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "❌ Неверный код привязки.",
                cancellationToken: cancellationToken);

            return;
        }

        var userId = message.From?.Id;

        if (userId is null)
            return;

        await _secretStore.SetSecretAsync(
            SecretKeys.TelegramAllowedUserId,
            userId.Value.ToString(),
            cancellationToken);

        _allowedUserId = userId.Value;
        _pairingCode = null;
        _agentStatus.PairingCode = null;
        _agentStatus.PairingCodeExpiresAt = null;

        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: "✅ PCRemote успешно привязан! Теперь ты можешь управлять компьютером.",
            replyMarkup: CreateMainMenu(),
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "PCRemote successfully paired with Telegram user {UserId}.",
            userId.Value);
    }
    
    private async Task RegisterBotCommandsAsync(
        CancellationToken cancellationToken)
    {
        await _botClient.SetMyCommands(
            [
                new BotCommand
                {
                    Command = "start",
                    Description = "Запустить бота и открыть меню"
                },
                new BotCommand
                {
                    Command = "help",
                    Description = "Список команд и помощь"
                },
                new BotCommand
                {
                    Command = "pair",
                    Description = "Привязать Telegram к компьютеру"
                },
                new BotCommand
                {
                    Command = "status",
                    Description = "Информация о компьютере"
                },
                new BotCommand
                {
                    Command = "screenshot",
                    Description = "Сделать скриншот экрана"
                },
                new BotCommand
                {
                    Command = "shutdown",
                    Description = "Выключить компьютер"
                },
                new BotCommand
                {
                    Command = "restart",
                    Description = "Перезагрузить компьютер"
                },
                new BotCommand
                {
                    Command = "sleep",
                    Description = "Перевести компьютер в сон"
                }
            ],
            cancellationToken: cancellationToken);
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

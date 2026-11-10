using PCRemote.Core.Interfaces.Storage;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace PCRemote.Agent.Configuration;

public class TelegramSetupService
{
    private readonly ISecretStore _secretStore;
    private readonly ILogger<TelegramSetupService> _logger;

    public TelegramSetupService(
        ISecretStore secretStore,
        ILogger<TelegramSetupService> logger)
    {
        _secretStore = secretStore;
        _logger = logger;
    }

    public async Task<bool> IsTokenConfiguredAsync(
        CancellationToken cancellationToken = default)
    {
        var token = await _secretStore.GetSecretAsync(
            SecretKeys.TelegramBotToken,
            cancellationToken);

        return !string.IsNullOrWhiteSpace(token);
    }

    public async Task<string> SetupAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException(
                "Telegram bot token cannot be empty.",
                nameof(token));
        }

        token = token.Trim();

        var botClient = new TelegramBotClient(token);

        try
        {
            var bot = await botClient.GetMe(cancellationToken);

            await _secretStore.SetSecretAsync(
                SecretKeys.TelegramBotToken,
                token,
                cancellationToken);

            _logger.LogInformation(
                "Telegram bot @{Username} connected and token saved.",
                bot.Username);

            return bot.Username ?? string.Empty;
        }
        catch (ApiRequestException exception)
            when (exception.ErrorCode == 401)
        {
            throw new InvalidOperationException(
                "Invalid Telegram bot token.",
                exception);
        }
    }
}
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

    public async Task SetupAsync(CancellationToken cancellationToken = default)
    {
        var existingToken = await _secretStore.GetSecretAsync(
            SecretKeys.TelegramBotToken,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(existingToken))
        {
            _logger.LogInformation("Telegram bot token was found.");
            return;
        }

        Console.WriteLine("=== PCRemote Setup ===");
        Console.WriteLine("Create your Telegram bot using @BotFather.");

        while (true)
        {
            Console.Write("Enter your Telegram bot token: ");

            var token = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("Token cannot be empty. Try again.");
                continue;
            }

            var botClient = new TelegramBotClient(token);

            try
            {
                var bot = await botClient.GetMe(cancellationToken);

                await _secretStore.SetSecretAsync(
                    SecretKeys.TelegramBotToken,
                    token,
                    cancellationToken);

                Console.WriteLine($"Bot @{bot.Username} connected successfully.");
                Console.WriteLine("Telegram bot token saved securely.");

                return;
            }
            catch (ApiRequestException exception) when (exception.ErrorCode == 401)
            {
                Console.WriteLine("Invalid Telegram bot token. Please try again.");
            }
        }
    }
}
using PCRemote.Core.Interfaces.Storage;

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
        Console.Write("Enter your Telegram bot token: ");

        var token = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "Telegram bot token cannot be empty.");
        }

        await _secretStore.SetSecretAsync(
            SecretKeys.TelegramBotToken,
            token,
            cancellationToken);

        Console.WriteLine("Telegram bot token saved successfully.");
    }
}
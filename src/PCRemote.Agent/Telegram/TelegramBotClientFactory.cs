using Telegram.Bot;

namespace PCRemote.Agent.Telegram;

public class TelegramBotClientFactory : ITelegramBotClientFactory
{
    public ITelegramBotClient Create(string token)
    {
        return new TelegramBotClient(token);
    }
}
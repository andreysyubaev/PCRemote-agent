using Telegram.Bot;

namespace PCRemote.Agent.Telegram;

public interface ITelegramBotClientFactory
{
    ITelegramBotClient Create(string token);
}
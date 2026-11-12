using PCRemote.Agent.Telegram;
using Telegram.Bot;

namespace PCRemote.Tests.TestDoubles;

public sealed class TestTelegramBotClientFactory : ITelegramBotClientFactory, IDisposable
{
    private readonly HttpClient _httpClient;

    public bool CreateCalled { get; private set; }

    public TestTelegramBotClientFactory(HttpMessageHandler handler)
    {
        _httpClient = new HttpClient(handler);
    }

    public ITelegramBotClient Create(string token)
    {
        CreateCalled = true;

        return new TelegramBotClient(token, _httpClient);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
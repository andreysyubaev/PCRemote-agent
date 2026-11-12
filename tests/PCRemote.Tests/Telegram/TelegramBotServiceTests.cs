
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PCRemote.Agent.Configuration;
using PCRemote.Agent.Telegram;
using PCRemote.Core.Models;
using PCRemote.Tests.TestDoubles;
using Telegram.Bot;

namespace PCRemote.Tests.Telegram;

public class TelegramBotServiceTests
{
    [Fact]
    public async Task StartAsync_WhenTokenIsMissing_ShouldNotCreateBotClient()
    {
        var secretStore = new FakeSecretStore();
        var factory = new TrackingTelegramBotClientFactory();
        var status = new AgentStatus();

        var service = new TelegramBotService(
            NullLogger<TelegramBotService>.Instance,
            secretStore,
            Options.Create(new TelegramSettings()),
            null!,
            null!,
            null!,
            status,
            factory);

        await service.StartAsync(CancellationToken.None);

        Assert.False(factory.CreateCalled);
        Assert.False(status.IsTelegramConnected);

        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }

    [Fact]
    public async Task StartAsync_WhenTelegramApiReturnsServerError_ShouldResetConnectionStatus()
    {
        var secretStore = new FakeSecretStore();

        await secretStore.SetSecretAsync(
            SecretKeys.TelegramBotToken,
            "123456:TEST_TOKEN");

        var handler = new TelegramApiErrorHandler();
        using var factory = new TestTelegramBotClientFactory(handler);

        var status = new AgentStatus
        {
            IsTelegramConnected = true
        };

        var service = new TelegramBotService(
            NullLogger<TelegramBotService>.Instance,
            secretStore,
            Options.Create(new TelegramSettings()),
            null!,
            null!,
            null!,
            status,
            factory);

        try
        {
            await service.StartAsync(CancellationToken.None);

            var timeout = DateTime.UtcNow.AddSeconds(5);

            while (
                status.IsTelegramConnected &&
                DateTime.UtcNow < timeout)
            {
                await Task.Delay(10);
            }

            Assert.True(factory.CreateCalled);
            Assert.True(handler.RequestCount > 0);
            Assert.False(status.IsTelegramConnected);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
            service.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnauthorizedUserSendsStatus_ShouldIgnoreMessage()
    {
        var secretStore = new FakeSecretStore();

        await secretStore.SetSecretAsync(
            SecretKeys.TelegramBotToken,
            "123456:TEST_TOKEN");

        await secretStore.SetSecretAsync(
            SecretKeys.TelegramAllowedUserId,
            "111111");

        var fakeClient = new FakeTelegramBotClient();
        var factory = new FakeTelegramBotClientFactory(fakeClient);
        var systemInfoProvider = new TrackingSystemInfoProvider();
        var status = new AgentStatus();

        var service = new TelegramBotService(
            NullLogger<TelegramBotService>.Instance,
            secretStore,
            Options.Create(new TelegramSettings()),
            systemInfoProvider,
            null!,
            null!,
            status,
            factory);

        try
        {
            await service.StartAsync(CancellationToken.None);

            var timeout = DateTime.UtcNow.AddSeconds(5);

            while (
                fakeClient.GetUpdatesCount < 2 &&
                DateTime.UtcNow < timeout)
            {
                await Task.Delay(10);
            }

            Assert.True(factory.CreateCalled);
            Assert.True(fakeClient.GetUpdatesCount >= 2);

            // Сохранённый разрешённый пользователь был загружен.
            Assert.True(status.IsPaired);

            // Бот не отправил ответ постороннему пользователю.
            Assert.Equal(0, fakeClient.SendMessageCount);

            // Бот не запрашивал системную информацию.
            Assert.Equal(0, systemInfoProvider.GetSystemInfoCallCount);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
            service.Dispose();
        }
    }

    private sealed class TrackingTelegramBotClientFactory
        : ITelegramBotClientFactory
    {
        public bool CreateCalled { get; private set; }

        public ITelegramBotClient Create(string token)
        {
            CreateCalled = true;

            throw new InvalidOperationException(
                "Telegram client should not be created without a token.");
        }
    }

    private sealed class FakeTelegramBotClientFactory(
        FakeTelegramBotClient client) : ITelegramBotClientFactory
    {
        public bool CreateCalled { get; private set; }

        public ITelegramBotClient Create(string token)
        {
            CreateCalled = true;
            return client;
        }
    }
}
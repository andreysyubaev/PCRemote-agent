
using Microsoft.Extensions.Logging.Abstractions;
using PCRemote.Agent.Configuration;
using PCRemote.Tests.TestDoubles;

namespace PCRemote.Tests.Configuration;

public class TelegramSetupServiceTests
{
    private readonly FakeSecretStore _secretStore = new();

    private TelegramSetupService CreateService()
    {
        return new TelegramSetupService(
            _secretStore,
            NullLogger<TelegramSetupService>.Instance);
    }

    [Fact]
    public async Task IsTokenConfiguredAsync_WhenTokenMissing_ShouldReturnFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.IsTokenConfiguredAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsTokenConfiguredAsync_WhenTokenExists_ShouldReturnTrue()
    {
        // Arrange
        await _secretStore.SetSecretAsync(
            SecretKeys.TelegramBotToken,
            "test-token");

        var service = CreateService();

        // Act
        var result = await service.IsTokenConfiguredAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task RemoveConfigurationAsync_ShouldDeleteTelegramSecrets()
    {
        // Arrange
        await _secretStore.SetSecretAsync(
            SecretKeys.TelegramBotToken,
            "test-token");

        await _secretStore.SetSecretAsync(
            SecretKeys.TelegramAllowedUserId,
            "123456");

        var service = CreateService();

        // Act
        await service.RemoveConfigurationAsync();

        // Assert
        Assert.False(await service.IsTokenConfiguredAsync());

        Assert.Null(await _secretStore.GetSecretAsync(
            SecretKeys.TelegramAllowedUserId));
    }

    [Fact]
    public async Task SetupAsync_WhenTokenIsEmpty_ShouldThrowArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var action = () => service.SetupAsync(" ");

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(action);
    }
}

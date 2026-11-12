using PCRemote.Core.Models;

namespace PCRemote.Tests;

public class AgentStatusTests
{
    [Fact]
    public void IsRunning_WhenChanged_ShouldRaisePropertyChanged()
    {
        // Arrange
        var status = new AgentStatus();
        string? changedProperty = null;

        status.PropertyChanged += (_, args) =>
        {
            changedProperty = args.PropertyName;
        };

        // Act
        status.IsRunning = true;

        // Assert
        Assert.True(status.IsRunning);
        Assert.Equal(nameof(AgentStatus.IsRunning), changedProperty);
    }

    [Fact]
    public void IsRunning_WhenSetToSameValue_ShouldNotRaisePropertyChanged()
    {
        // Arrange
        var status = new AgentStatus();
        var eventCount = 0;

        status.PropertyChanged += (_, _) => eventCount++;

        // Act
        status.IsRunning = false;

        // Assert
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void PairingCode_WhenChanged_ShouldRaisePropertyChanged()
    {
        // Arrange
        var status = new AgentStatus();
        string? changedProperty = null;

        status.PropertyChanged += (_, args) =>
        {
            changedProperty = args.PropertyName;
        };

        // Act
        status.PairingCode = "123456";

        // Assert
        Assert.Equal("123456", status.PairingCode);
        Assert.Equal(nameof(AgentStatus.PairingCode), changedProperty);
    }

    [Fact]
    public void BotUsername_WhenChanged_ShouldRaisePropertyChanged()
    {
        // Arrange
        var status = new AgentStatus();
        var eventCount = 0;

        status.PropertyChanged += (_, _) => eventCount++;

        // Act
        status.BotUsername = "my_test_bot";

        // Assert
        Assert.Equal(1, eventCount);
    }
    
    [Fact]
    public void PairingCodeExpiresAt_WhenChanged_ShouldRaisePropertyChanged()
    {
        var status = new AgentStatus();
        var eventCount = 0;

        status.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AgentStatus.PairingCodeExpiresAt))
                eventCount++;
        };

        status.PairingCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5);

        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void IsTelegramConnected_WhenChanged_ShouldRaisePropertyChanged()
    {
        var status = new AgentStatus();
        var eventCount = 0;

        status.PropertyChanged += (_, _) => eventCount++;

        status.IsTelegramConnected = true;

        Assert.True(status.IsTelegramConnected);
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void IsPaired_WhenChanged_ShouldRaisePropertyChanged()
    {
        var status = new AgentStatus();
        var eventCount = 0;

        status.PropertyChanged += (_, _) => eventCount++;

        status.IsPaired = true;

        Assert.True(status.IsPaired);
        Assert.Equal(1, eventCount);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123456")]
    public void PairingCode_WhenSetToSameValue_ShouldNotRaisePropertyChanged(
        string? value)
    {
        // Arrange
        var status = new AgentStatus
        {
            PairingCode = value
        };

        var eventCount = 0;

        status.PropertyChanged += (_, _) => eventCount++;

        // Act
        status.PairingCode = value;

        // Assert
        Assert.Equal(0, eventCount);
    }
}
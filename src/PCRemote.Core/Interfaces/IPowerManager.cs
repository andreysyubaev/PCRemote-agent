namespace PCRemote.Core.Interfaces;

public interface IPowerManager
{
    Task ShutdownAsync(CancellationToken cancellationToken = default);

    Task RestartAsync(CancellationToken cancellationToken = default);

    Task SleepAsync(CancellationToken cancellationToken = default);
}
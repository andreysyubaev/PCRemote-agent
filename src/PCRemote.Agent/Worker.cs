using PCRemote.Core.Interfaces;

namespace PCRemote.Agent;

public class Worker(ILogger<Worker> logger, ISystemInfoProvider systemInfoProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation("PCRemote Agent started.");

        try
        {
            var systemInfo = await systemInfoProvider.GetSystemInfoAsync(
                stoppingToken);

            logger.LogInformation("Operating System: {OS}",
                systemInfo.OperatingSystem);

            logger.LogInformation("OS Version: {Version}",
                systemInfo.OSVersion);

            logger.LogInformation("Processor: {Processor}",
                systemInfo.Processor);

            logger.LogInformation("Graphics Card: {GPU}",
                systemInfo.GraphicCard);

            logger.LogInformation("Total RAM: {RAM} GB",
                systemInfo.TotalMemoryBytes / 1024.0 / 1024 / 1024);

            logger.LogInformation("Free Disk Space: {Disk:F2} GB",
                systemInfo.FreeDiskSpaceBytes / 1024.0 / 1024 / 1024);

            while (!stoppingToken.IsCancellationRequested)
                await Task.Delay(1000, stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("PCRemote Agent is stopping.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "An error occurred while running PCRemote Agent.");

            throw;
        }
    }
}

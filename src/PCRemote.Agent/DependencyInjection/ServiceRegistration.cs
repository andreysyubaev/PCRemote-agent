using PCRemote.Core.Interfaces;
using PCRemote.Platform.MacOS.Power;
using PCRemote.Platform.MacOS.Screenshots;
using PCRemote.Platform.MacOS.SystemInformation;
using PCRemote.Platform.Windows.Power;
using PCRemote.Platform.Windows.Screenshots;
using PCRemote.Platform.Windows.SystemInformation;
using PCRemote.Core.Interfaces.Storage;
using PCRemote.Platform.MacOS.Security;
using PCRemote.Platform.Windows.Security;

namespace PCRemote.Agent.DependencyInjection;

public static class ServiceRegistration
{
    public static IServiceCollection AddPlatformServices(
        this IServiceCollection services)
    {
        if (OperatingSystem.IsMacOS())
        {
            services.AddSingleton<IPowerManager, MacPowerManager>();
            services.AddSingleton<ISystemInfoProvider, MacSystemInfoProvider>();
            services.AddSingleton<IScreenshotService, MacScreenshotService>();
            services.AddSingleton<ISecretStore, MacSecretStore>();
        }
        else if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IPowerManager, WindowsPowerManager>();
            services.AddSingleton<ISystemInfoProvider, WindowsSystemInfoProvider>();
            services.AddSingleton<IScreenshotService, WindowsScreenshotService>();
            services.AddSingleton<ISecretStore, WindowsSecretStore>();
        }
        else
        {
            throw new PlatformNotSupportedException(
                "This operating system is not supported.");
        }

        return services;
    }
}
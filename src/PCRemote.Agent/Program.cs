using PCRemote.Agent.DependencyInjection;
using PCRemote.Agent.Configuration;
using PCRemote.Agent.Telegram;
using PCRemote.Agent.Runtime;

namespace PCRemote.Agent;

public static class AgentHost
{
    public static IHost Build(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddPlatformServices();

        builder.Services.AddSingleton<TelegramSetupService>();

        builder.Services.Configure<TelegramSettings>(
            builder.Configuration.GetSection(TelegramSettings.SectionName));

        builder.Services.AddSingleton<Worker>();
        builder.Services.AddSingleton<TelegramBotService>();
        
        builder.Services.AddSingleton<AgentRuntime>();

        return builder.Build();
    }
}

public static class Program
{
    public static async Task Main(string[] args)
    {
        var host = AgentHost.Build(args);

        var setupService =
            host.Services.GetRequiredService<TelegramSetupService>();

        if (!await setupService.IsTokenConfiguredAsync())
        {
            Console.WriteLine("=== PCRemote Setup ===");
            Console.WriteLine("Create your Telegram bot using @BotFather.");
            Console.Write("Enter your Telegram bot token: ");

            var token = Console.ReadLine();

            await setupService.SetupAsync(token ?? string.Empty);
        }

        var runtime = host.Services.GetRequiredService<AgentRuntime>();

        await host.StartAsync();
        await runtime.StartAsync();

        await host.WaitForShutdownAsync();

        await runtime.StopAsync();
    }
}
using System;
using System.IO;
using Microsoft.Extensions.Logging;
using PCRemote.Agent.DependencyInjection;
using PCRemote.Agent.Configuration;
using PCRemote.Agent.Telegram;
using PCRemote.Agent.Runtime;
using PCRemote.Core.Models;
using Serilog;
using PCRemote.Agent.Logging;
using PCRemote.Core.Interfaces.Logging;

namespace PCRemote.Agent;

public static class AgentHost
{
    public static IHost Build(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        var logsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PCRemote",
            "Logs");

        Directory.CreateDirectory(logsDirectory);
        
        var logWatcher = new FileLogWatcher();

        var logFilePath = Path.Combine(
            logsDirectory,
            "pcremote-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Sink(
                new LogNotificationSink(logWatcher.NotifyLogsChanged))
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Services.AddSerilog();

        builder.Services.AddPlatformServices();

        builder.Services.AddSingleton<TelegramSetupService>();
        
        builder.Services.Configure<TelegramSettings>(
            builder.Configuration.GetSection(TelegramSettings.SectionName));

        builder.Services.AddTransient<Worker>();
        builder.Services.AddTransient<TelegramBotService>();

        builder.Services.AddSingleton<AgentRuntime>();
        builder.Services.AddSingleton<AgentStatus>();
        
        builder.Services.AddSingleton<ILogReader, FileLogReader>();
        builder.Services.AddSingleton<ILogWatcher>(logWatcher);

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
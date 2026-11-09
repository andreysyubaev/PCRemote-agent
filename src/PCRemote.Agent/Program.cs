using PCRemote.Agent;
using PCRemote.Agent.DependencyInjection;
using PCRemote.Agent.Configuration;
using PCRemote.Agent.Telegram;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPlatformServices();

builder.Services.AddSingleton<TelegramSetupService>();

builder.Services
    .AddOptions<TelegramSettings>()
    .Bind(builder.Configuration.GetSection(TelegramSettings.SectionName))
    // .Validate(
    //     settings => !string.IsNullOrWhiteSpace(settings.BotToken),
    //     "Telegram bot token is required.")
    .Validate(
        settings => settings.AllowedUserId > 0,
        "Telegram user ID must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddHostedService<Worker>();

builder.Services.AddHostedService<TelegramBotService>();

var host = builder.Build();

var setupService = host.Services.GetRequiredService<TelegramSetupService>();

await setupService.SetupAsync();

await host.RunAsync();
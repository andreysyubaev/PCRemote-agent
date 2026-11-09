using PCRemote.Agent;
using PCRemote.Agent.DependencyInjection;
using PCRemote.Agent.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPlatformServices();

builder.Services
    .AddOptions<TelegramSettings>()
    .Bind(builder.Configuration.GetSection(TelegramSettings.SectionName))
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.BotToken),
        "Telegram bot token is required.")
    .Validate(
        settings => settings.AllowedUserId > 0,
        "Telegram user ID must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();
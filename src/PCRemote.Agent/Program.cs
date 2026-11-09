using PCRemote.Agent;
using PCRemote.Agent.DependencyInjection;
using PCRemote.Agent.Configuration;
using PCRemote.Agent.Telegram;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPlatformServices();

builder.Services.AddSingleton<TelegramSetupService>();

builder.Services.Configure<TelegramSettings>(
    builder.Configuration.GetSection(TelegramSettings.SectionName));

builder.Services.AddHostedService<Worker>();

builder.Services.AddHostedService<TelegramBotService>();

var host = builder.Build();

var setupService = host.Services.GetRequiredService<TelegramSetupService>();

await setupService.SetupAsync();

await host.RunAsync();
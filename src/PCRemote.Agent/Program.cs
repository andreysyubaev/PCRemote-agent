using PCRemote.Agent;
using PCRemote.Agent.DependencyInjection;
using PCRemote.Agent.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPlatformServices();

builder.Services.Configure<TelegramSettings>(
    builder.Configuration.GetSection(TelegramSettings.SectionName));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();
using PCRemote.Agent;
using PCRemote.Agent.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPlatformServices();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();
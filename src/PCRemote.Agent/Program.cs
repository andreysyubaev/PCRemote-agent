using PCRemote.Agent;
using PCRemote.Agent.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.AddPlatformServices();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

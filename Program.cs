using Cocona;
using Microsoft.Extensions.DependencyInjection;
using SecretKeeper.Commands;
using SecretKeeper.Services;

var builder = CoconaApp.CreateBuilder();

builder.Services.AddSingleton<SecretdService>();
builder.Services.AddSingleton<SystemdService>();
builder.Services.AddSingleton<LogWatcherService>();
builder.Services.AddSingleton<NotifierService>();

var app = builder.Build();

app.AddCommands<StartCommand>();
app.AddCommands<TestCommand>();

app.Run();
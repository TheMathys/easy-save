using System;
using System.Threading.Tasks;
using EasySave.Core.Interfaces;
using EasySave.ConsoleApp;
using Microsoft.Extensions.DependencyInjection;

string? envBasePath = Environment.GetEnvironmentVariable("EASYSAVE_BASE_PATH");
string basePath = !string.IsNullOrWhiteSpace(envBasePath)
    ? envBasePath
    : AppContext.BaseDirectory;

IServiceProvider serviceProvider = CompositionRoot.Build(basePath);

IBackupExecutor executor = serviceProvider.GetRequiredService<IBackupExecutor>();

Console.WriteLine($"EasySave console initialized with base path: {basePath}");

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Console.Cli;
using EasySave.Console.Tui;
using EasySave.Core.Interfaces;
using EasySave.ConsoleApp;
using Microsoft.Extensions.DependencyInjection;

class Program
{
    static async Task Main(string[] args)
    {
        string? envBasePath = Environment.GetEnvironmentVariable("EASYSAVE_BASE_PATH");
        string basePath = !string.IsNullOrWhiteSpace(envBasePath) ? envBasePath : AppContext.BaseDirectory;

        IServiceProvider serviceProvider = CompositionRoot.Build(basePath);

        if (CommandLineParser.ShouldRunTui(args))
        {
            await TuiRunner.RunAsync(serviceProvider);
            return;
        }

        IReadOnlyList<int> jobIds = CommandLineParser.Parse(args);

        if (!jobIds.Any())
        {
            string usage = "Usage: EasySave.exe <jobIds> (ex: 1-3 ou 1;3;5)";
            Console.WriteLine(usage);
            return;
        }

        IBackupExecutor executor = serviceProvider.GetRequiredService<IBackupExecutor>();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true; 
            cts.Cancel();
        };

        Console.WriteLine($"EasySave console initialized with base path: {basePath}");
        Console.WriteLine($"Executing jobs: {string.Join(", ", jobIds)}");

        try
        {
            await executor.ExecuteAsync(jobIds, cts.Token);
            Console.WriteLine("Backup jobs completed successfully."); 
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Backup cancelled by user (Ctrl+C)."); 
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Backup failed: {ex.Message}"); 
        }
    }
}
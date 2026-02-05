using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Console.Cli;
using EasySave.Console.Resources;
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
            string usage = $"{LangHelper.GetString("UsageJob")}";
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

        Console.WriteLine($"{LangHelper.GetString("ConsoleInitialized")}: {basePath}");
        Console.WriteLine($"{LangHelper.GetString("ExecutingJobs")}: {string.Join(", ", jobIds)}");

        try
        {
            await executor.ExecuteAsync(jobIds, cts.Token);
            Console.WriteLine(LangHelper.GetString("BackupSuccess")); 
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine(LangHelper.GetString("BackupCancelled")); 
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{LangHelper.GetString("BackupError")}: {ex.Message}"); 
        }
    }
}
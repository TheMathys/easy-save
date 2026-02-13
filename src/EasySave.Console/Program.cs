using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Console.Cli;
using EasySave.Console.Tui;
using EasySave.Console.Resources;
using EasySave.Console;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Core.Exceptions;
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

        long lastProgressTicks = 0;
        const int ProgressThrottleMs = 120;
        var progress = new Progress<BackupProgress>(p =>
        {
            if (p?.State != BackupState.Active) return;
            long now = Environment.TickCount64;
            if (now - lastProgressTicks >= ProgressThrottleMs)
            {
                lastProgressTicks = now;
                ProgressDisplay.WriteProgressLine(p);
            }
        });

        try
        {
            await executor.ExecuteAsync(jobIds, progress, cts.Token);
            ProgressDisplay.ClearProgressLine();
            Console.WriteLine(LangHelper.GetString("BackupSuccess"));
        }
        catch (OperationCanceledException)
        {
            ProgressDisplay.ClearProgressLine();
            Console.WriteLine(LangHelper.GetString("BackupCancelled"));
        }
        catch (BusinessSoftwareDetectedException)
        {
            ProgressDisplay.ClearProgressLine();
            Console.WriteLine(LangHelper.GetString("BusinessSoftwareDetected"));
        }
        catch (Exception ex)
        {
            ProgressDisplay.ClearProgressLine();
            Console.WriteLine($"{LangHelper.GetString("BackupError")}: {ex.Message}");
        }
    }
}
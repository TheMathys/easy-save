using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Console.Resources;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Core.Exceptions;
using EasySave.Core.Interfaces;
using ProgressDisplay = EasySave.Console.ProgressDisplay;

namespace EasySave.Console.Tui
{
    /// <summary>Option Lancer les sauvegardes (option 3).</summary>
    public static partial class TuiRunner
    {
        private static async Task RunJobsAsync(IConfigurationRepository configRepository, IBackupExecutor backupExecutor)
        {
            System.Console.WriteLine();
            string? runTitle = LangHelper.GetString("RunJobsTitle");
            System.Console.WriteLine(runTitle ?? "=== Run Backups ===");

            BackupConfiguration? config = await configRepository.LoadAsync(CancellationToken.None);
            if (config == null || config.Jobs.Count == 0)
            {
                string? noJobsMsg = LangHelper.GetString("NoJobsFound");
                System.Console.WriteLine(noJobsMsg ?? "No backup jobs found.");
                return;
            }

            await ListJobsAsync(configRepository);

            string? selectJobsPrompt = LangHelper.GetString("SelectJobsPrompt");
            System.Console.Write($"{selectJobsPrompt ?? "Enter job IDs to run (e.g., 1-3 or 1,3,5)"}: ");
            string? input = System.Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                string? invalidInputMsg = LangHelper.GetString("InvalidInput");
                System.Console.WriteLine(invalidInputMsg ?? "Invalid input. No jobs selected.");
                return;
            }

            List<int> jobIds = new List<int>();
            string[] parts = input.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Contains('-'))
                {
                    string[] rangeParts = trimmed.Split('-');
                    if (rangeParts.Length == 2 &&
                        int.TryParse(rangeParts[0].Trim(), out int start) &&
                        int.TryParse(rangeParts[1].Trim(), out int end))
                    {
                        for (int i = Math.Min(start, end); i <= Math.Max(start, end); i++)
                        {
                            if (i >= 1 && !jobIds.Contains(i))
                                jobIds.Add(i);
                        }
                    }
                }
                else if (int.TryParse(trimmed, out int id) && id >= 1 && !jobIds.Contains(id))
                {
                    jobIds.Add(id);
                }
            }

            // Filter job IDs to those actually present in configuration
            var existingIds = new HashSet<int>(config.Jobs.Select(j => j.Id));
            jobIds = jobIds.Where(id => existingIds.Contains(id)).ToList();

            if (jobIds.Count == 0)
            {
                string? invalidInputMsg = LangHelper.GetString("InvalidInput");
                System.Console.WriteLine(invalidInputMsg ?? "Invalid job IDs. No jobs selected.");
                return;
            }

            using CancellationTokenSource cts = new CancellationTokenSource();
            System.Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            string? backupStartMsg = LangHelper.GetString("BackupStart");
            System.Console.WriteLine(backupStartMsg ?? "Starting backup...");

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
                await backupExecutor.ExecuteAsync(jobIds, progress, cts.Token);
                ProgressDisplay.ClearProgressLine();
                string? backupCompletedMsg = LangHelper.GetString("BackupCompleted");
                System.Console.WriteLine(backupCompletedMsg ?? "Backup completed successfully.");
            }
            catch (OperationCanceledException)
            {
                ProgressDisplay.ClearProgressLine();
                string? backupCancelMsg = LangHelper.GetString("BackupCancel");
                System.Console.WriteLine(backupCancelMsg ?? "Backup cancelled by user.");
            }
            catch (BusinessSoftwareDetectedException)
            {
                ProgressDisplay.ClearProgressLine();
                string? msg = LangHelper.GetString("BusinessSoftwareDetected");
                System.Console.WriteLine(msg ?? "Backup blocked: business software is running.");
            }
            catch (Exception ex)
            {
                ProgressDisplay.ClearProgressLine();
                string? backupErrorMsg = LangHelper.GetString("BackupError");
                System.Console.WriteLine($"{backupErrorMsg ?? "Backup failed"}: {ex.Message}");
            }
        }
    }
}

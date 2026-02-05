using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Console.Resources;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace EasySave.Console.Tui
{
    /// <summary>
    /// Runs the Text User Interface (TUI) menu loop for EasySave.
    /// </summary>
    public static class TuiRunner
    {
        /// <summary>
        /// Runs the TUI menu loop, resolving required services from the provided service provider.
        /// </summary>
        /// <param name="provider">Service provider to resolve IConfigurationRepository and IBackupExecutor.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task RunAsync(IServiceProvider provider)
        {
            IConfigurationRepository configRepository = provider.GetRequiredService<IConfigurationRepository>();
            IBackupExecutor backupExecutor = provider.GetRequiredService<IBackupExecutor>();

            bool running = true;
            while (running)
            {
                DisplayMenu();
                string? choice = System.Console.ReadLine()?.Trim().ToLowerInvariant();

                switch (choice)
                {
                    case "1":
                        await CreateJobAsync(configRepository);
                        break;
                    case "2":
                        await ListJobsAsync(configRepository);
                        break;
                    case "3":
                        await RunJobsAsync(configRepository, backupExecutor);
                        break;
                    case "4":
                        ShowHelp();
                        break;
                    case "0":
                    case "q":
                    case "quit":
                        running = false;
                        break;
                    default:
                        string? errorMsg = LangHelper.GetString("MenuInvalidChoice");
                        System.Console.WriteLine(errorMsg ?? "Invalid choice. Please select 1-4 or 0 to quit.");
                        break;
                }

                if (running && !System.Console.IsInputRedirected && !System.Console.IsOutputRedirected)
                {
                    System.Console.WriteLine();
                    System.Console.WriteLine(LangHelper.GetString("PressKeyContinue"));
                    System.Console.ReadKey();
                    System.Console.Clear();
                }
            }
        }

        private static void DisplayMenu()
        {
            string? title = LangHelper.GetString("MenuTitle");
            string? option1 = LangHelper.GetString("MenuOption1");
            string? option2 = LangHelper.GetString("MenuOption2");
            string? option3 = LangHelper.GetString("MenuOption3");
            string? option4 = LangHelper.GetString("MenuOption4");
            string? option0 = LangHelper.GetString("MenuOption0");
            string? prompt = LangHelper.GetString("MenuPrompt");

            System.Console.WriteLine(title ?? "=== EasySave Menu ===");
            System.Console.WriteLine();
            System.Console.WriteLine($"1. {option1 ?? "Create a backup job"}");
            System.Console.WriteLine($"2. {option2 ?? "List backup jobs"}");
            System.Console.WriteLine($"3. {option3 ?? "Run backups"}");
            System.Console.WriteLine($"4. {option4 ?? "Help"}");
            System.Console.WriteLine($"0. {option0 ?? "Quit"}");
            System.Console.WriteLine();
            System.Console.Write(prompt ?? "Enter your choice: ");
        }

        private static async Task CreateJobAsync(IConfigurationRepository configRepository)
        {
            System.Console.WriteLine();
            string? createTitle = LangHelper.GetString("CreateJobTitle");
            System.Console.WriteLine(createTitle ?? "=== Create Backup Job ===");

            BackupConfiguration? config = await configRepository.LoadAsync(CancellationToken.None);
            if (config == null)
            {
                config = new BackupConfiguration
                {
                    Jobs = new List<BackupJob>(),
                    LastFullBackupUtcByJobId = new Dictionary<int, DateTime>()
                };
            }

            List<BackupJob> jobs = config.Jobs.ToList();

            if (jobs.Count >= 5)
            {
                string? maxJobsMsg = LangHelper.GetString("MaxJobsReached");
                System.Console.WriteLine(maxJobsMsg ?? "Maximum number of jobs (5) reached. Cannot create more jobs.");
                return;
            }

            int newJobId = jobs.Count > 0 ? jobs.Max(j => j.Id) + 1 : 1;
            if (newJobId > 5)
            {
                string? maxJobsMsg = LangHelper.GetString("MaxJobsReached");
                System.Console.WriteLine(maxJobsMsg ?? "Maximum number of jobs (5) reached. Cannot create more jobs.");
                return;
            }

            string? namePrompt = LangHelper.GetString("BackupName");
            System.Console.Write($"{namePrompt ?? "Enter backup name"}: ");
            string? name = System.Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                string? invalidInputMsg = LangHelper.GetString("InvalidInput");
                System.Console.WriteLine(invalidInputMsg ?? "Invalid input. Job creation cancelled.");
                return;
            }

            string? sourcePrompt = LangHelper.GetString("SourceDirectory");
            System.Console.Write($"{sourcePrompt ?? "Enter source directory"}: ");
            string? sourcePath = System.Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                string? invalidInputMsg = LangHelper.GetString("InvalidInput");
                System.Console.WriteLine(invalidInputMsg ?? "Invalid input. Job creation cancelled.");
                return;
            }

            string? targetPrompt = LangHelper.GetString("TargetDirectory");
            System.Console.Write($"{targetPrompt ?? "Enter target directory"}: ");
            string? targetPath = System.Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                string? invalidInputMsg = LangHelper.GetString("InvalidInput");
                System.Console.WriteLine(invalidInputMsg ?? "Invalid input. Job creation cancelled.");
                return;
            }

            string? typePrompt = LangHelper.GetString("BackupTypeSelect");
            System.Console.Write($"{typePrompt ?? "Select backup type (1=Full, 2=Differential)"}: ");
            string? typeInput = System.Console.ReadLine()?.Trim();
            BackupType backupType;
            if (typeInput == "1" || string.Equals(typeInput, "full", StringComparison.OrdinalIgnoreCase))
            {
                backupType = BackupType.Full;
            }
            else if (typeInput == "2" || string.Equals(typeInput, "differential", StringComparison.OrdinalIgnoreCase))
            {
                backupType = BackupType.Differential;
            }
            else
            {
                string? invalidInputMsg = LangHelper.GetString("InvalidInput");
                System.Console.WriteLine(invalidInputMsg ?? "Invalid backup type. Job creation cancelled.");
                return;
            }

            BackupJob newJob = new BackupJob
            {
                Id = newJobId,
                Name = name,
                SourcePath = sourcePath,
                TargetPath = targetPath,
                Type = backupType
            };

            jobs.Add(newJob);

            BackupConfiguration updatedConfig = new BackupConfiguration
            {
                LogAndStateDirectory = config.LogAndStateDirectory,
                Jobs = jobs,
                LastFullBackupUtcByJobId = config.LastFullBackupUtcByJobId
            };

            await configRepository.SaveAsync(updatedConfig, CancellationToken.None);

            string? jobCreatedMsg = LangHelper.GetString("JobCreated");
            if (!string.IsNullOrWhiteSpace(jobCreatedMsg))
            {
                System.Console.WriteLine(string.Format(jobCreatedMsg, newJobId));
            }
            else
            {
                System.Console.WriteLine($"Job {newJobId} created successfully.");
            }
        }

        private static async Task ListJobsAsync(IConfigurationRepository configRepository)
        {
            System.Console.WriteLine();
            string? listTitle = LangHelper.GetString("ListJobsTitle");
            System.Console.WriteLine(listTitle ?? "=== Backup Jobs ===");

            BackupConfiguration? config = await configRepository.LoadAsync(CancellationToken.None);
            if (config == null || config.Jobs.Count == 0)
            {
                string? noJobsMsg = LangHelper.GetString("NoJobsFound");
                System.Console.WriteLine(noJobsMsg ?? "No backup jobs found.");
                return;
            }

            foreach (BackupJob job in config.Jobs.OrderBy(j => j.Id))
            {
                string? fullBackup = LangHelper.GetString("FullBackup");
                string? differentialBackup = LangHelper.GetString("DifferentialBackup");
                string typeStr = job.Type == BackupType.Full
                    ? (fullBackup ?? "Full")
                    : (differentialBackup ?? "Differential");

                System.Console.WriteLine($"Job {job.Id}: {job.Name}");
                System.Console.WriteLine($"  Type: {typeStr}");
                System.Console.WriteLine($"  Source: {job.SourcePath}");
                System.Console.WriteLine($"  Target: {job.TargetPath}");
                System.Console.WriteLine();
            }
        }

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
                            if (i >= 1 && i <= 5 && !jobIds.Contains(i))
                                jobIds.Add(i);
                        }
                    }
                }
                else if (int.TryParse(trimmed, out int id) && id >= 1 && id <= 5 && !jobIds.Contains(id))
                {
                    jobIds.Add(id);
                }
            }

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

            try
            {
                await backupExecutor.ExecuteAsync(jobIds, cts.Token);
                string? backupCompletedMsg = LangHelper.GetString("BackupCompleted");
                System.Console.WriteLine(backupCompletedMsg ?? "Backup completed successfully.");
            }
            catch (OperationCanceledException)
            {
                string? backupCancelMsg = LangHelper.GetString("BackupCancel");
                System.Console.WriteLine(backupCancelMsg ?? "Backup cancelled by user.");
            }
            catch (Exception ex)
            {
                string? backupErrorMsg = LangHelper.GetString("BackupError");
                System.Console.WriteLine($"{backupErrorMsg ?? "Backup failed"}: {ex.Message}");
            }
        }

        private static void ShowHelp()
        {
            System.Console.WriteLine();
            string? helpTitle = LangHelper.GetString("HelpTitle");
            System.Console.WriteLine(helpTitle ?? "=== Help ===");
            System.Console.WriteLine();

            string? helpText = LangHelper.GetString("HelpText");
            if (!string.IsNullOrWhiteSpace(helpText))
            {
                System.Console.WriteLine(helpText);
            }
            else
            {
                System.Console.WriteLine("EasySave - Backup Management System");
                System.Console.WriteLine();
                System.Console.WriteLine("Options:");
                System.Console.WriteLine("  1. Create a backup job - Add a new backup configuration");
                System.Console.WriteLine("  2. List backup jobs - Display all configured backup jobs");
                System.Console.WriteLine("  3. Run backups - Execute one or more backup jobs");
                System.Console.WriteLine("  4. Help - Show this help message");
                System.Console.WriteLine("  0. Quit - Exit the application");
                System.Console.WriteLine();
                System.Console.WriteLine("When running backups, you can specify job IDs as:");
                System.Console.WriteLine("  - Single ID: 1");
                System.Console.WriteLine("  - Range: 1-3");
                System.Console.WriteLine("  - List: 1,3,5 or 1;3;5");
            }
        }
    }
}

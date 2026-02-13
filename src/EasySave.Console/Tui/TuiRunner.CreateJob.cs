using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Console.Resources;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Core.Interfaces;

namespace EasySave.Console.Tui
{
    /// <summary>Option Créer un travail (option 1) et helpers pour le type de sauvegarde.</summary>
    public static partial class TuiRunner
    {
        private static BackupType ReadBackupType()
        {
            if (!System.Console.IsInputRedirected)
                return ShowBackupTypeInteractiveMenu();

            while (true)
            {
                string? typePrompt = LangHelper.GetString("BackupTypeSelect");
                System.Console.WriteLine();
                System.Console.WriteLine(typePrompt ?? "Select backup type (1=Full, 2=Differential):");
                System.Console.WriteLine("1. " + (LangHelper.GetString("FullBackup") ?? "Full"));
                System.Console.WriteLine("2. " + (LangHelper.GetString("DifferentialBackup") ?? "Differential"));
                System.Console.Write("> ");

                string? typeInput = System.Console.ReadLine()?.Trim();
                if (TryParseBackupType(typeInput, out BackupType backupType))
                    return backupType;

                string? invalidInputMsg = LangHelper.GetString("InvalidInput");
                System.Console.WriteLine(invalidInputMsg ?? "Invalid backup type. Please choose 1 or 2.");
            }
        }

        private static bool TryParseBackupType(string? input, out BackupType backupType)
        {
            backupType = BackupType.Full;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            string value = input.Trim();
            if (value == "1" || string.Equals(value, "full", StringComparison.OrdinalIgnoreCase))
            {
                backupType = BackupType.Full;
                return true;
            }

            if (value == "2" || string.Equals(value, "differential", StringComparison.OrdinalIgnoreCase))
            {
                backupType = BackupType.Differential;
                return true;
            }

            return false;
        }

        private static BackupType ShowBackupTypeInteractiveMenu()
        {
            int selectedIndex = 0; // 0 = Full, 1 = Differential

            while (true)
            {
                System.Console.Clear();

                string? typePrompt = LangHelper.GetString("BackupTypeSelect");
                string? full = LangHelper.GetString("FullBackup");
                string? differential = LangHelper.GetString("DifferentialBackup");

                System.Console.WriteLine(typePrompt ?? "Select backup type:");
                System.Console.WriteLine();

                WriteBackupTypeLine(0, selectedIndex, "1", full ?? "Full");
                WriteBackupTypeLine(1, selectedIndex, "2", differential ?? "Differential");

                string? hint = LangHelper.GetString("TuiNavigationHint");
                System.Console.WriteLine();
                System.Console.WriteLine(hint ?? "Use arrows to move, Enter/Space to validate, Esc to cancel.");

                ConsoleKeyInfo keyInfo = System.Console.ReadKey(intercept: true);
                switch (keyInfo.Key)
                {
                    case ConsoleKey.UpArrow:
                        selectedIndex = (selectedIndex + 2 - 1) % 2;
                        break;
                    case ConsoleKey.DownArrow:
                        selectedIndex = (selectedIndex + 1) % 2;
                        break;
                    case ConsoleKey.Enter:
                    case ConsoleKey.Spacebar:
                        return selectedIndex == 0 ? BackupType.Full : BackupType.Differential;
                    case ConsoleKey.Escape:
                        return BackupType.Full;
                    default:
                        char c = keyInfo.KeyChar;
                        if (c != '\0' && TryParseBackupType(c.ToString(), out BackupType parsed))
                            return parsed;
                        break;
                }
            }
        }

        private static void WriteBackupTypeLine(int index, int selectedIndex, string number, string text)
        {
            string prefix = index == selectedIndex ? ">" : " ";
            System.Console.WriteLine($"{prefix} {number}. {text}");
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

            int newJobId = jobs.Count > 0 ? jobs.Max(j => j.Id) + 1 : 1;

            string? namePrompt = LangHelper.GetString("BackupName");
            System.Console.Write($"{namePrompt ?? "Enter backup name"}: ");
            string? name = System.Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                string? invalidInputMsg = LangHelper.GetString("InvalidInput");
                System.Console.WriteLine(invalidInputMsg ?? "Invalid input. Job creation cancelled.");
                return;
            }

            string? sourcePath = ReadPathLine(LangHelper.GetString("SourceDirectory"), "Enter source directory");
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                string? invalidInputMsg = LangHelper.GetString("InvalidInput");
                System.Console.WriteLine(invalidInputMsg ?? "Invalid input. Job creation cancelled.");
                return;
            }

            string? targetPath = ReadPathLine(LangHelper.GetString("TargetDirectory"), "Enter target directory");
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                string? invalidInputMsg = LangHelper.GetString("InvalidInput");
                System.Console.WriteLine(invalidInputMsg ?? "Invalid input. Job creation cancelled.");
                return;
            }

            BackupType backupType = ReadBackupType();

            string? extPrompt = LangHelper.GetString("ExcludeExtensionsPrompt");
            System.Console.WriteLine();
            System.Console.Write(extPrompt ?? "Exclude file extensions (comma-separated, example .tmp,.log). Leave empty for none: ");
            string? extInput = System.Console.ReadLine()?.Trim();
            List<string> excludeExtensions = ParseCommaSeparatedList(extInput);

            string? dirPrompt = LangHelper.GetString("ExcludeDirectoryNamesPrompt");
            System.Console.Write(dirPrompt ?? "Exclude directory names (comma-separated). Leave empty for none: ");
            string? dirInput = System.Console.ReadLine()?.Trim();
            List<string> excludeDirectoryNames = ParseCommaSeparatedList(dirInput);

            BackupJob newJob = new BackupJob
            {
                Id = newJobId,
                Name = name,
                SourcePath = sourcePath,
                TargetPath = targetPath,
                Type = backupType,
                ExcludeExtensions = excludeExtensions,
                ExcludeDirectoryNames = excludeDirectoryNames
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
                System.Console.WriteLine(string.Format(jobCreatedMsg, newJobId));
            else
                System.Console.WriteLine($"Job {newJobId} created successfully.");
        }
    }
}

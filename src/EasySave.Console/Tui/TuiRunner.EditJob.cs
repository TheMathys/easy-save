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
    /// <summary>Option "Modifier un travail" : saisie ID, champs pré-remplis, sauvegarde.</summary>
    public static partial class TuiRunner
    {
        private static async Task EditJobAsync(IConfigurationRepository configRepository)
        {
            System.Console.WriteLine();
            string? prompt = LangHelper.GetString("TuiPromptEditJobId");
            System.Console.Write(prompt ?? "Enter the ID of the job to edit: ");
            string? raw = System.Console.ReadLine()?.Trim();
            if (!int.TryParse(raw, out int jobId) || jobId < 1)
            {
                string? invalid = LangHelper.GetString("InvalidInput");
                System.Console.WriteLine(invalid ?? "Invalid input. Cancelled.");
                return;
            }

            BackupConfiguration? config = await configRepository.LoadAsync(CancellationToken.None);
            if (config == null || config.Jobs.Count == 0)
            {
                string? noJobs = LangHelper.GetString("NoJobsFound");
                System.Console.WriteLine(noJobs ?? "No backup jobs found.");
                return;
            }

            BackupJob? job = config.Jobs.FirstOrDefault(j => j.Id == jobId);
            if (job == null)
            {
                string? noJobs = LangHelper.GetString("NoJobsFound");
                System.Console.WriteLine(noJobs ?? "No job with this ID.");
                return;
            }

            string? namePrompt = LangHelper.GetString("BackupName");
            System.Console.Write($"{namePrompt ?? "Name"} [{job.Name}]: ");
            string? name = System.Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(name)) name = job.Name;

            string? sourcePath = ReadPathLine(LangHelper.GetString("SourceDirectory"), "Source directory");
            if (string.IsNullOrWhiteSpace(sourcePath)) sourcePath = job.SourcePath;

            string? targetPath = ReadPathLine(LangHelper.GetString("TargetDirectory"), "Target directory");
            if (string.IsNullOrWhiteSpace(targetPath)) targetPath = job.TargetPath;

            System.Console.Write($"Backup type (1=Full, 2=Differential) [{job.Type}] (Enter to keep): ");
            string? typeInput = System.Console.ReadLine()?.Trim();
            BackupType backupType = job.Type;
            if (!string.IsNullOrWhiteSpace(typeInput) && TryParseBackupType(typeInput, out BackupType parsed))
                backupType = parsed;

            string? extPrompt = LangHelper.GetString("ExcludeExtensionsPrompt");
            string currentExt = string.Join(", ", job.ExcludeExtensions ?? Array.Empty<string>());
            System.Console.Write($"{extPrompt ?? "Exclude extensions (comma-separated)"} [{currentExt}]: ");
            string? extInput = System.Console.ReadLine()?.Trim();
            List<string> excludeExtensions = string.IsNullOrWhiteSpace(extInput)
                ? (job.ExcludeExtensions?.ToList() ?? new List<string>())
                : ParseCommaSeparatedList(extInput);

            string? dirPrompt = LangHelper.GetString("ExcludeDirectoryNamesPrompt");
            string currentDirs = string.Join(", ", job.ExcludeDirectoryNames ?? Array.Empty<string>());
            System.Console.Write($"{dirPrompt ?? "Exclude directory names (comma-separated)"} [{currentDirs}]: ");
            string? dirInput = System.Console.ReadLine()?.Trim();
            List<string> excludeDirectoryNames = string.IsNullOrWhiteSpace(dirInput)
                ? (job.ExcludeDirectoryNames?.ToList() ?? new List<string>())
                : ParseCommaSeparatedList(dirInput);

            BackupJob updatedJob = new BackupJob
            {
                Id = job.Id,
                Name = name,
                SourcePath = sourcePath,
                TargetPath = targetPath,
                Type = backupType,
                ExcludeExtensions = excludeExtensions,
                ExcludeDirectoryNames = excludeDirectoryNames
            };

            List<BackupJob> newJobs = config.Jobs.Select(j => j.Id == jobId ? updatedJob : j).ToList();
            BackupConfiguration updatedConfig = new BackupConfiguration
            {
                LogAndStateDirectory = config.LogAndStateDirectory,
                Jobs = newJobs,
                LastFullBackupUtcByJobId = config.LastFullBackupUtcByJobId
            };
            await configRepository.SaveAsync(updatedConfig, CancellationToken.None);

            string? updatedMsg = LangHelper.GetString("TuiJobUpdated");
            System.Console.WriteLine(string.Format(updatedMsg ?? "Job {0} has been updated.", jobId));
        }
    }
}

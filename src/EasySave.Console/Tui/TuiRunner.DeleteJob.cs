using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Console.Resources;
using EasySave.Core.Entities;
using EasySave.Core.Interfaces;

namespace EasySave.Console.Tui
{
    /// <summary>
    /// Handles the \"Delete job\" menu option: asks for job ID, confirms, then updates configuration and LastFullBackupUtcByJobId.
    /// </summary>
    public static partial class TuiRunner
    {
        private static async Task DeleteJobAsync(IConfigurationRepository configRepository)
        {
            System.Console.WriteLine();
            string? prompt = LangHelper.GetString("TuiPromptDeleteJobId");
            System.Console.Write(prompt ?? "Enter the ID of the job to delete: ");
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

            string? confirmMsg = LangHelper.GetString("TuiConfirmDelete");
            System.Console.Write(string.Format(confirmMsg ?? "Delete job \"{0}\"? (y/n): ", job.Name));
            string? answer = System.Console.ReadLine()?.Trim().ToLowerInvariant();
            bool confirmed = answer == "y" || answer == "yes" || answer == "o" || answer == "oui";
            if (!confirmed)
            {
                System.Console.WriteLine("Cancelled.");
                return;
            }

            List<BackupJob> newJobs = config.Jobs.Where(j => j.Id != jobId).ToList();
            Dictionary<int, DateTime> newLastFull = config.LastFullBackupUtcByJobId
                .Where(kv => kv.Key != jobId)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            BackupConfiguration updated = new BackupConfiguration
            {
                LogAndStateDirectory = config.LogAndStateDirectory,
                LogFileFormat = config.LogFileFormat,
                LogDestination = config.LogDestination,
                CentralizedLogServerAddress = config.CentralizedLogServerAddress,
                Jobs = newJobs,
                LastFullBackupUtcByJobId = newLastFull,
                EncryptExtensions = config.EncryptExtensions,
                PriorityExtensions = config.PriorityExtensions,
                EncryptionKeyPath = config.EncryptionKeyPath,
                BusinessSoftwareProcessName = config.BusinessSoftwareProcessName,
                LargeFileThresholdKb = config.LargeFileThresholdKb
            };
            await configRepository.SaveAsync(updated, CancellationToken.None);

            string? deletedMsg = LangHelper.GetString("TuiJobDeleted");
            System.Console.WriteLine(string.Format(deletedMsg ?? "Job {0} has been deleted.", jobId));
        }
    }
}

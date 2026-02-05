using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Console.Resources;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Core.Interfaces;

namespace EasySave.Console.Tui
{
    /// <summary>Option Lister les travaux (option 2).</summary>
    public static partial class TuiRunner
    {
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
    }
}

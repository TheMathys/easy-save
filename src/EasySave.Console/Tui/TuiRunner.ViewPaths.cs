using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Console.Resources;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Core.Interfaces;

namespace EasySave.Console.Tui
{
    /// <summary>TUI option "View paths (config and logs)" and change log format.</summary>
    public static partial class TuiRunner
    {
        /// <summary>
        /// Displays the configuration/state/log paths and allows the user
        /// to view and change the current log file format if the user wants to (JSON/XML).
        /// </summary>
        /// <param name="configRepository">
        /// Repository used to load and persist the global backup configuration,
        /// including the selected log file format.
        /// </param>
        /// <param name="paths">
        /// Paths helper providing the base directory used for configuration,
        /// state file and daily log files.
        /// </param>
        private static async Task ViewPathsAsync(IConfigurationRepository configRepository, EasySavePaths paths)
        {
            System.Console.WriteLine();
            string? configLabel = LangHelper.GetString("TuiViewPathsConfig");
            System.Console.WriteLine(configLabel ?? "Config, state and log directory:");
            System.Console.WriteLine(paths.BaseDirectory);
            System.Console.WriteLine();
            string? hint = LangHelper.GetString("TuiViewPathsHint");
            System.Console.WriteLine(hint ?? "backup-config.json, state.json and daily log files (yyyy-MM-dd.json / yyyy-MM-dd.xml) are stored in this directory.");

            // Display current log format.
            BackupConfiguration? config = await configRepository.LoadAsync(CancellationToken.None).ConfigureAwait(false);
            LogFileFormat currentFormat = config?.LogFileFormat ?? LogFileFormat.Json;
            System.Console.WriteLine();
            System.Console.WriteLine($"Current log format: {currentFormat}");

            System.Console.Write(LangHelper.GetString("TuiChangeLogFormatPrompt")
                                 ?? "Change log format? (json/xml, Enter to keep): ");
            string? raw = System.Console.ReadLine()?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(raw))
                return;

            LogFileFormat newFormat;
            if (raw is "json" or "j")
                newFormat = LogFileFormat.Json;
            else if (raw is "xml" or "x")
                newFormat = LogFileFormat.Xml;
            else
            {
                string? invalid = LangHelper.GetString("InvalidInput");
                System.Console.WriteLine(invalid ?? "Invalid input. Log format unchanged.");
                return;
            }

            if (newFormat == currentFormat)
                return;

            // If no configuration exists yet, create a minimal one.
            if (config == null)
            {
                config = new BackupConfiguration
                {
                    LogAndStateDirectory = paths.BaseDirectory,
                    Jobs = Array.Empty<BackupJob>(),
                    LastFullBackupUtcByJobId = new Dictionary<int, DateTime>(),
                    LargeFileThresholdKb = null,
                    EncryptExtensions = Array.Empty<string>(),
                    PriorityExtensions = Array.Empty<string>()
                };
            }

            BackupConfiguration updated = new BackupConfiguration
            {
                LogAndStateDirectory = config.LogAndStateDirectory,
                LogFileFormat = newFormat,
                LogDestination = config.LogDestination,
                CentralizedLogServerAddress = config.CentralizedLogServerAddress,
                Jobs = config.Jobs,
                LastFullBackupUtcByJobId = config.LastFullBackupUtcByJobId,
                EncryptExtensions = config.EncryptExtensions,
                PriorityExtensions = config.PriorityExtensions,
                EncryptionKeyPath = config.EncryptionKeyPath,
                BusinessSoftwareProcessName = config.BusinessSoftwareProcessName,
                LargeFileThresholdKb = config.LargeFileThresholdKb
            };

            await configRepository.SaveAsync(updated, CancellationToken.None).ConfigureAwait(false);

            string? done = LangHelper.GetString("TuiLogFormatUpdated");
            System.Console.WriteLine(done ?? $"Log format updated to {newFormat}.");
        }
    }
}

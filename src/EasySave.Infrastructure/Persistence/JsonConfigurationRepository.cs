using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Core.Interfaces;
using System.Text.Json;

namespace EasySave.Infrastructure.Persistence
{
    /// <summary>
    /// Loads and saves the application configuration in JSON format.
    /// No hard-coded paths: the configuration directory is injected.
    /// </summary>
    public sealed class JsonConfigurationRepository : IConfigurationRepository
    {
        private readonly SemaphoreSlim _configUpdateLock = new(1, 1);

        /// <summary>
        /// JSON serialization options (indented output, camelCase naming).
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly Func<string> _getConfigDirectory;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonConfigurationRepository"/> class.
        /// </summary>
        /// <param name="configDirectory">
        /// Directory where the configuration file is stored.
        /// </param>
        public JsonConfigurationRepository(string configDirectory)
            : this(() => configDirectory!)
        {
            if (configDirectory == null)
                throw new ArgumentNullException(nameof(configDirectory));
        }

        /// <summary>
        /// Initializes a new instance with a delegate to resolve the config directory at runtime.
        /// Used when the path can change (e.g. GUI changing base path without losing data).
        /// </summary>
        /// <param name="getConfigDirectory">Delegate returning the current config directory.</param>
        public JsonConfigurationRepository(Func<string> getConfigDirectory)
        {
            _getConfigDirectory = getConfigDirectory ?? throw new ArgumentNullException(nameof(getConfigDirectory));
        }

        /// <summary>
        /// Loads the backup configuration from the JSON file.
        /// </summary>
        /// <param name="cancellationToken">
        /// Token used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// The loaded <see cref="BackupConfiguration"/>, or null if the file does not exist
        /// or cannot be deserialized.
        /// </returns>
        public async Task<BackupConfiguration?> LoadAsync(CancellationToken cancellationToken = default)
        {
            string configDirectory = _getConfigDirectory();
            string configFilePath = Path.Combine(configDirectory, "backup-config.json");
            if (!File.Exists(configFilePath))
                return null;

            ConfigDto? dto;
            try
            {
                var json = await File.ReadAllTextAsync(configFilePath, cancellationToken).ConfigureAwait(false);
                dto = JsonSerializer.Deserialize<ConfigDto>(json, JsonOptions);
            }
            catch (JsonException)
            {
                // Malformed JSON: return null as per method documentation.
                return null;
            }

            if (dto == null) return null;

            var jobs = dto.Jobs?.Select((j, i) => new BackupJob
            {
                Id = j.Id ?? (i + 1),
                Name = j.Name ?? "",
                SourcePath = j.SourcePath ?? "",
                TargetPath = j.TargetPath ?? "",
                Type = string.Equals(j.Type, "Differential", StringComparison.OrdinalIgnoreCase)
                    ? Core.Enums.BackupType.Differential
                    : Core.Enums.BackupType.Full,
                ExcludeExtensions = j.ExcludeExtensions ?? new List<string>(),
                ExcludeDirectoryNames = j.ExcludeDirectoryNames ?? new List<string>()
            }).ToList() ?? new List<BackupJob>();

            var lastFull = dto.LastFullBackupUtcByJobId ?? new Dictionary<int, DateTime>();

            LogFileFormat format = LogFileFormat.Json;
            if (!string.IsNullOrWhiteSpace(dto.LogFileFormat))
            {
                if (Enum.TryParse<LogFileFormat>(dto.LogFileFormat, ignoreCase: true, out var parsed))
                    format = parsed;
            }

            LogDestination logDestination = LogDestination.Local;
            if (!string.IsNullOrWhiteSpace(dto.LogDestination) && Enum.TryParse<LogDestination>(dto.LogDestination, ignoreCase: true, out var destParsed))
                logDestination = destParsed;

            List<string> encryptExtensions = dto.EncryptExtensions ?? [];
            List<string> priorityExtensions = dto.PriorityExtensions ?? [];
            int? largeFileThresholdKb = dto.LargeFileThresholdKb;
            if (largeFileThresholdKb.HasValue && largeFileThresholdKb.Value <= 0)
            {
                largeFileThresholdKb = null;
            }

            bool useDarkTheme = dto.UseDarkTheme ?? false;
            int textScalePercent = dto.TextScalePercent is >= 75 and <= 150 ? dto.TextScalePercent.Value : 100;

            return new BackupConfiguration
            {
                LogAndStateDirectory = dto.LogAndStateDirectory ?? configDirectory,
                LogFileFormat = format,
                LogDestination = logDestination,
                CentralizedLogServerAddress = string.IsNullOrWhiteSpace(dto.CentralizedLogServerAddress) ? null : dto.CentralizedLogServerAddress.Trim(),
                Jobs = jobs,
                LastFullBackupUtcByJobId = lastFull,
                EncryptExtensions = encryptExtensions,
                PriorityExtensions = priorityExtensions,
                EncryptionKeyPath = string.IsNullOrWhiteSpace(dto.EncryptionKeyPath) ? null : dto.EncryptionKeyPath.Trim(),
                BusinessSoftwareProcessName = string.IsNullOrWhiteSpace(dto.BusinessSoftwareProcessName) ? null : dto.BusinessSoftwareProcessName.Trim(),
                LargeFileThresholdKb = largeFileThresholdKb,
                UseDarkTheme = useDarkTheme,
                TextScalePercent = textScalePercent
            };
        }

        /// <summary>
        /// Saves the backup configuration to the JSON file.
        /// </summary>
        /// <param name="backupConfiguration">Configuration to persist.</param>
        /// <param name="cancellationToken">
        /// Token used to cancel the asynchronous operation.
        /// </param>
        public async Task SaveAsync(BackupConfiguration backupConfiguration, CancellationToken cancellationToken = default)
        {
            string configDirectory = _getConfigDirectory();
            Directory.CreateDirectory(configDirectory);
            string configFilePath = Path.Combine(configDirectory, "backup-config.json");

            var dto = new ConfigDto
            {
                LogAndStateDirectory = backupConfiguration.LogAndStateDirectory,
                LogFileFormat = backupConfiguration.LogFileFormat.ToString(),
                LogDestination = backupConfiguration.LogDestination.ToString(),
                CentralizedLogServerAddress = backupConfiguration.CentralizedLogServerAddress,
                EncryptExtensions = backupConfiguration.EncryptExtensions?.ToList() ?? new List<string>(),
                PriorityExtensions = backupConfiguration.PriorityExtensions?.ToList() ?? new List<string>(),
                EncryptionKeyPath = backupConfiguration.EncryptionKeyPath,
                BusinessSoftwareProcessName = backupConfiguration.BusinessSoftwareProcessName,
                LargeFileThresholdKb = backupConfiguration.LargeFileThresholdKb,
                UseDarkTheme = backupConfiguration.UseDarkTheme,
                TextScalePercent = backupConfiguration.TextScalePercent,
                Jobs = backupConfiguration.Jobs.Select(j => new JobDto
                {
                    Id = j.Id,
                    Name = j.Name,
                    SourcePath = j.SourcePath,
                    TargetPath = j.TargetPath,
                    Type = j.Type == Core.Enums.BackupType.Differential
                        ? "Differential"
                        : "Full",
                    ExcludeExtensions = j.ExcludeExtensions?.Count > 0 ? new List<string>(j.ExcludeExtensions) : null,
                    ExcludeDirectoryNames = j.ExcludeDirectoryNames?.Count > 0 ? new List<string>(j.ExcludeDirectoryNames) : null
                }).ToList(),
                LastFullBackupUtcByJobId = backupConfiguration.LastFullBackupUtcByJobId
                    .ToDictionary(k => k.Key, v => v.Value)
            };

            var json = JsonSerializer.Serialize(dto, JsonOptions);
            await File.WriteAllTextAsync(configFilePath, json, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates the timestamp of the last full backup for a given job.
        /// </summary>
        /// <param name="jobId">Identifier of the backup job.</param>
        /// <param name="utc">UTC date and time of the last full backup.</param>
        /// <param name="cancellationToken">
        /// Token used to cancel the asynchronous operation.
        /// </param>
        public async Task UpdateLastFullBackupAsync(
            int jobId,
            DateTime utc,
            CancellationToken cancellationToken = default)
        {
            await _configUpdateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var config = await LoadAsync(cancellationToken).ConfigureAwait(false);
                if (config == null) return;
                var dict = config.LastFullBackupUtcByJobId
                    .ToDictionary(k => k.Key, v => v.Value);
                dict[jobId] = utc;
                var updated = new BackupConfiguration
                {
                    LogAndStateDirectory = config.LogAndStateDirectory,
                    LogFileFormat = config.LogFileFormat,
                    LogDestination = config.LogDestination,
                    CentralizedLogServerAddress = config.CentralizedLogServerAddress,
                    Jobs = config.Jobs,
                    LastFullBackupUtcByJobId = dict,
                    EncryptExtensions = config.EncryptExtensions,
                    PriorityExtensions = config.PriorityExtensions,
                    EncryptionKeyPath = config.EncryptionKeyPath,
                    BusinessSoftwareProcessName = config.BusinessSoftwareProcessName,
                    LargeFileThresholdKb = config.LargeFileThresholdKb,
                    UseDarkTheme = config.UseDarkTheme,
                    TextScalePercent = config.TextScalePercent
                };
                await SaveAsync(updated, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _configUpdateLock.Release();
            }
        }

        /// <summary>
        /// Data Transfer Object used for JSON serialization of the configuration.
        /// </summary>
        private sealed class ConfigDto
        {
            public string? LogAndStateDirectory { get; set; }
            public string? LogFileFormat { get; set; }
            public string? LogDestination { get; set; }
            public string? CentralizedLogServerAddress { get; set; }
            public List<string>? EncryptExtensions { get; set; }
            public List<string>? PriorityExtensions { get; set; }
            public string? EncryptionKeyPath { get; set; }
            public string? BusinessSoftwareProcessName { get; set; }
            public int? LargeFileThresholdKb { get; set; }
            public bool? UseDarkTheme { get; set; }
            public int? TextScalePercent { get; set; }
            public List<JobDto>? Jobs { get; set; }
            public Dictionary<int, DateTime>? LastFullBackupUtcByJobId { get; set; }
        }

        /// <summary>
        /// Data Transfer Object used for JSON serialization of a backup job.
        /// </summary>
        private sealed class JobDto
        {
            public int? Id { get; set; }
            public string? Name { get; set; }
            public string? SourcePath { get; set; }
            public string? TargetPath { get; set; }
            public string? Type { get; set; }
            public List<string>? ExcludeExtensions { get; set; }
            public List<string>? ExcludeDirectoryNames { get; set; }
        }
    }
}

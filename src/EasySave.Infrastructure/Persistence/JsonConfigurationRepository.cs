using EasySave.Core.Entities;
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
        private readonly string _configDirectory;
        private readonly string _configFilePath;
        private readonly SemaphoreSlim _configUpdateLock = new(1, 1);

        /// <summary>
        /// JSON serialization options (indented output, camelCase naming).
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonConfigurationRepository"/> class.
        /// </summary>
        /// <param name="configDirectory">
        /// Directory where the configuration file is stored.
        /// </param>
        public JsonConfigurationRepository(string configDirectory)
        {
            _configDirectory = configDirectory ?? throw new ArgumentNullException(nameof(configDirectory));
            _configFilePath = Path.Combine(_configDirectory, "backup-config.json");
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
            if (!File.Exists(_configFilePath))
                return null;

            ConfigDto? dto;
            try
            {
                var json = await File.ReadAllTextAsync(_configFilePath, cancellationToken).ConfigureAwait(false);
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
                    : Core.Enums.BackupType.Full
            }).ToList() ?? new List<BackupJob>();

            var lastFull = dto.LastFullBackupUtcByJobId ?? new Dictionary<int, DateTime>();

            return new BackupConfiguration
            {
                LogAndStateDirectory = dto.LogAndStateDirectory ?? _configDirectory,
                Jobs = jobs,
                LastFullBackupUtcByJobId = lastFull
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
            Directory.CreateDirectory(_configDirectory);

            var dto = new ConfigDto
            {
                LogAndStateDirectory = backupConfiguration.LogAndStateDirectory,
                Jobs = backupConfiguration.Jobs.Select(j => new JobDto
                {
                    Id = j.Id,
                    Name = j.Name,
                    SourcePath = j.SourcePath,
                    TargetPath = j.TargetPath,
                    Type = j.Type == Core.Enums.BackupType.Differential
                        ? "Differential"
                        : "Full"
                }).ToList(),
                LastFullBackupUtcByJobId = backupConfiguration.LastFullBackupUtcByJobId
                    .ToDictionary(k => k.Key, v => v.Value)
            };

            var json = JsonSerializer.Serialize(dto, JsonOptions);
            await File.WriteAllTextAsync(_configFilePath, json, cancellationToken).ConfigureAwait(false);
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
                    Jobs = config.Jobs,
                    LastFullBackupUtcByJobId = dict
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
        }
    }
}

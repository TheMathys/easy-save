using EasySave.Core.Entities;
using EasySave.Core.Interfaces;
using System.Text.Json;

namespace EasySave.Infrastructure.Persistence
{
    /// <summary>
    /// Implementation of IStateWriter that writes backup jobs state to a JSON file.
    /// </summary>
    public class JsonStateWriter : IStateWriter
    {
        private readonly Func<string> _getStateFilePath;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Initializes a new instance of the JsonStateWriter class.
        /// </summary>
        /// <param name="stateFilePath">The full path to the state.json file.</param>
        /// <exception cref="ArgumentNullException">Thrown if stateFilePath is null or empty.</exception>
        public JsonStateWriter(string stateFilePath)
            : this(() => stateFilePath ?? throw new ArgumentNullException(nameof(stateFilePath)))
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
                throw new ArgumentNullException(nameof(stateFilePath), "The state file path cannot be null or empty.");
        }

        /// <summary>
        /// Initializes a new instance with a delegate to resolve the state file path at runtime.
        /// Used when the path can change (e.g. GUI changing base path without losing data).
        /// </summary>
        /// <param name="getStateFilePath">Delegate returning the current state file path.</param>
        public JsonStateWriter(Func<string> getStateFilePath)
        {
            _getStateFilePath = getStateFilePath ?? throw new ArgumentNullException(nameof(getStateFilePath));

            // Initialize JSON options once for performance
            _jsonOptions = new JsonSerializerOptions
            {
                // CamelCase formatting (e.g., "SourceDirectory" -> "sourceDirectory")
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                // Compact format (no indentation/spaces) to save disk space and write faster
                WriteIndented = false
            };
        }

        /// <summary>
        /// Asynchronously writes the list of backup progress to the JSON file.
        /// This overwrites the existing file content with the new state.
        /// </summary>
        public async Task WriteStateAsync(IReadOnlyList<BackupProgress> progressList, CancellationToken cancellationToken = default)
        {
            string stateFilePath = _getStateFilePath();
            if (string.IsNullOrWhiteSpace(stateFilePath))
                throw new InvalidOperationException("The state file path cannot be null or empty.");
            var directory = Path.GetDirectoryName(stateFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            StateDto dto = new StateDto
            {
                UpdatedAt = DateTime.UtcNow,
                Jobs = progressList.Select(p => new JobProgressDto
                {
                    BackupName = p.BackupName,
                    LastActionTimestamp = p.LastActionTimestamp,
                    State = p.State.ToString(),
                    TotalFilesCount = p.TotalFilesCount,
                    TotalSizeBytes = p.TotalSizeBytes,
                    ProgressPercent = p.ProgressPercent,
                    RemainingFilesCount = p.RemainingFilesCount,
                    RemainingSizeBytes = p.RemainingSizeBytes,
                    CurrentSourcePath = p.CurrentSourcePath,
                    CurrentDestinationPath = p.CurrentDestinationPath,
                    EstimatedTimeRemainingSeconds = p.EstimatedTimeRemainingSeconds
                }).ToList()
            };
            string json = JsonSerializer.Serialize(dto, _jsonOptions) + Environment.NewLine;
            await File.WriteAllTextAsync(stateFilePath, json, cancellationToken).ConfigureAwait(false);
        }

        private sealed class StateDto
        {
            public DateTime UpdatedAt { get; set; }
            public List<JobProgressDto> Jobs { get; set; } = new();
        }

        private sealed class JobProgressDto
        {
            public string BackupName { get; set; } = "";
            public DateTime LastActionTimestamp { get; set; }
            public string State { get; set; } = "";
            public int TotalFilesCount { get; set; }
            public long TotalSizeBytes { get; set; }
            public double ProgressPercent { get; set; }
            public int RemainingFilesCount { get; set; }
            public long RemainingSizeBytes { get; set; }
            public string? CurrentSourcePath { get; set; }
            public string? CurrentDestinationPath { get; set; }
            public double? EstimatedTimeRemainingSeconds { get; set; }
        }
    }
}
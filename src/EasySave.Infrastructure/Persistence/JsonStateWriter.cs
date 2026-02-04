using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Core.Interfaces;
using EasySave.Core.Models;

namespace EasySave.Infrastructure.Persistence
{
	/// <summary>
	/// Implementation of IStateWriter that writes backup jobs state to a JSON file.
	/// </summary>
	public class JsonStateWriter : IStateWriter
	{
		private readonly string _stateFilePath;
		private readonly JsonSerializerOptions _jsonOptions;

		/// <summary>
		/// Initializes a new instance of the JsonStateWriter class.
		/// </summary>
		/// <param name="stateFilePath">The full path to the state.json file.</param>
		/// <exception cref="ArgumentNullException">Thrown if stateFilePath is null or empty.</exception>
		public JsonStateWriter(string stateFilePath)
		{
			if (string.IsNullOrWhiteSpace(stateFilePath))
			{
				throw new ArgumentNullException(nameof(stateFilePath), "The state file path cannot be null or empty.");
			}

			_stateFilePath = stateFilePath;

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
			// Ensure the directory exists to avoid DirectoryNotFoundException
			var directory = Path.GetDirectoryName(_stateFilePath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			// Create (or overwrite) the file and serialize the data
			// FileStream is used here for async capabilities
			using FileStream createStream = File.Create(_stateFilePath);

			await JsonSerializer.SerializeAsync(createStream, progressList, _jsonOptions, cancellationToken);
		}
	}
}
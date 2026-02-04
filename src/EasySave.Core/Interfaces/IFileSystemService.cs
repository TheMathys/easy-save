using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Core.Models;

namespace EasySave.Core.Interfaces
{
	/// <summary>
	/// Defines operations for interacting with the file system.
	/// Acts as an abstraction layer to handle file enumeration, copying, and metadata retrieval.
	/// </summary>
	public interface IFileSystemService
	{
        /// <summary>
        /// Recursively enumerates all files in a source directory.
        /// Returns an async stream to process files one by one without loading everything into memory.
        /// </summary>
        /// <param name="sourcePath">The root directory path to scan.</param>
        /// <param name="cancellationToken">Token to cancel the enumeration operation.</param>
        /// <returns>An async stream of <see cref="FileItem"/>.</returns>
        IAsyncEnumerable<FileItem> EnumerateFilesAsync(string sourcePath, CancellationToken cancellationToken = default);

		/// <summary>
		/// Asynchronously copies a single file from source to destination.
		/// </summary>
		/// <param name="sourcePath">The absolute path of the source file.</param>
		/// <param name="destinationPath">The absolute path of the destination file.</param>
		/// <param name="cancellationToken">Token to cancel the copy operation.</param>
		/// <returns>
		/// A <see cref="long"/> representing the time taken in milliseconds. 
		/// Returns a negative value if an error occurred.
		/// </returns>
		Task<long> CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);

		/// <summary>
		/// Converts a local path to a UNC path (Universal Naming Convention) if necessary.
		/// Useful for network drives or standardized logging.
		/// </summary>
		/// <param name="path">The file system path.</param>
		/// <returns>The standardized UNC path string.</returns>
		string GetUncPath(string path);

		/// <summary>
		/// Gets the size of a specific file in bytes.
		/// </summary>
		/// <param name="path">The absolute path of the file.</param>
		/// <returns>The file size in bytes.</returns>
		long GetFileSize(string path);

		/// <summary>
		/// Ensures that a directory structure exists. Creates it if it does not.
		/// </summary>
		/// <param name="directoryPath">The directory path to check/create.</param>
		void EnsureDirectoryExists(string directoryPath);

		/// <summary>
		/// Retrieves the last modification date and time of a file in UTC.
		/// </summary>
		/// <param name="path">The absolute path of the file.</param>
		/// <returns>The last write time in UTC.</returns>
		DateTime GetLastWriteTimeUtc(string path);
	}
}
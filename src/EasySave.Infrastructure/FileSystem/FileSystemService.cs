using EasySave.Core.Interfaces;
using EasySave.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using EasySave.Core.Interfaces;
using EasySave.Core.Models;
using System.Runtime.CompilerServices;
using System.Text;

namespace EasySave.Infrastructure.FileSystem
{
    /// <summary>
    /// Implementation of file system operations (local, external, network).
    /// </summary>
    public sealed class FileSystemService : IFileSystemService
    {
        public void EnsureDirectoryExists(string directoryPath)
        {
            // Check if directory exists before attempting creation
            FileInfo fi = new FileInfo(directoryPath);
            if (!fi.Directory.Exists)
            {
                // Directory.CreateDirectory creates all parent directories atomically
                Directory.CreateDirectory(directoryPath);
            }
        }

        public async IAsyncEnumerable<FileItem> EnumerateFilesAsync(string sourcePath, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // Early exit if source doesn't exist
            if (!Directory.Exists(sourcePath))
                yield break;

            // Stack-based DFS traversal (memory efficient for deep hierarchies)
            var stack = new Stack<string>();
            stack.Push(sourcePath);

            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = stack.Pop();

                // Yield all files in current directory
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fi = new FileInfo(file);
                    var relativePath = Path.GetRelativePath(sourcePath, file);
                    yield return new FileItem(relativePath, file, fi.LastWriteTimeUtc);
                }

                // Push subdirectories for recursive traversal
                foreach (var dir in Directory.EnumerateDirectories(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    stack.Push(dir);
                }
            }

            // Allow async method completion (no-op for yield return)
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public long GetFileSize(string path)
        {
            // donne la taille du fichier
            FileInfo fi = new FileInfo(path);
            return fi.Length;
        }

        public DateTime GetLastWriteTimeUtc(string path)
        {
            // donne la dernière date d'écriture
            FileInfo fi = new FileInfo(path);
            return fi.LastWriteTimeUtc;
        }

        public string GetUncPath(string path)
        {
            // donne le chemin universel windows
            FileInfo fi = new FileInfo(path);
            return fi.FullName;
        }
        
        public async Task<long> CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Ensure destination parent directory exists
                string? dir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    EnsureDirectoryExists(dir);
                }

                // Copy the source file to the destination (overwrite if it already exists)
                await Task.Run(() => File.Copy(sourcePath, destinationPath, true), cancellationToken);
                stopwatch.Stop();
    
                // Return the transfer time in milliseconds
                return stopwatch.ElapsedMilliseconds;
            }
            catch
            {
                stopwatch.Stop();
                // Return a negative elapsed time to indicate an error
                return -stopwatch.ElapsedMilliseconds;
            }
        }
    }
}
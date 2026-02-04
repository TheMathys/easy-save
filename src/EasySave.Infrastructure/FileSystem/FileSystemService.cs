using EasySave.Core.Interfaces;
using EasySave.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Runtime.CompilerServices;

namespace EasySave.Infrastructure.FileSystem
{
    /// <summary>
    /// Implementation of file system operations (local, external, network).
    /// </summary>
    public sealed class FileSystemService : IFileSystemService
    {
        public void EnsureDirectoryExists(string directoryPath)
        {
            FileInfo fi = new FileInfo(directoryPath);
            if (!fi.Directory.Exists)
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        public async IAsyncEnumerable<FileItem> EnumerateFilesAsync(string sourcePath, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (!Directory.Exists(sourcePath))
                yield break;

            var stack = new Stack<string>();
            stack.Push(sourcePath);

            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = stack.Pop();

                foreach (var file in Directory.EnumerateFiles(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fi = new FileInfo(file);
                    var relativePath = Path.GetRelativePath(sourcePath, file);
                    yield return new FileItem(relativePath, file, fi.LastWriteTimeUtc);
                }

                foreach (var dir in Directory.EnumerateDirectories(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    stack.Push(dir);
                }
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        public long GetFileSize(string path)
        {
            FileInfo fi = new FileInfo(path);
            return fi.Length;
        }

        public DateTime GetLastWriteTimeUtc(string path)
        {
            FileInfo fi = new FileInfo(path);
            return fi.LastWriteTimeUtc;
        }

        public string GetUncPath(string path)
        {
            FileInfo fi = new FileInfo(path);
            return fi.FullName;
        }
        
        public async Task<long> CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                string? dir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    EnsureDirectoryExists(dir);
                }

                await Task.Run(() => File.Copy(sourcePath, destinationPath, true), cancellationToken);
                stopwatch.Stop();
    
                return stopwatch.ElapsedMilliseconds;
            }
            catch
            {
                stopwatch.Stop();
                return -stopwatch.ElapsedMilliseconds;
            }
        }
    }
}
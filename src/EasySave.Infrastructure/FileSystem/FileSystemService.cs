using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using EasySave.Core.Interfaces;
using EasySave.Core.Models;

namespace EasySave.Infrastructure.FileSystem
{
    public class FileSystemService:IFileSystemService
    {
        public IAsyncEnumerable<FileItem> EnumerateFilesAsync(string sourceDirectory)
        {
            throw new NotImplementedException();
        }

        Task<long> IFileSystemService.CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
        {
            return CopyFileAsync(sourcePath, destinationPath, cancellationToken);
        }

        public string GetUncPath(string path)
        {
            throw new NotImplementedException();
        }

        public long GetFileSize(string path)
        {
            throw new NotImplementedException();
        }

        public void EnsureDirectoryExists(string directoryPath)
        {
            throw new NotImplementedException();
        }

        public DateTime GetLastWriteTimeUtc(string path)
        {
            throw new NotImplementedException();
        }
        
        async Task<long> CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                string? dir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
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
using EasySave.Core.Interfaces;
using EasySave.Core.Models;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace EasySave.Infrastructure.FileSystem
{
    /// <summary>
    /// Implementation of file system operations (local, external, network).
    /// </summary>
    public sealed class FileSystemService : IFileSystemService
    {
        public Task<long> CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public void EnsureDirectoryExists(string directoryPath)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public DateTime GetLastWriteTimeUtc(string path)
        {
            throw new NotImplementedException();
        }

        public string GetUncPath(string path)
        {
            throw new NotImplementedException();
        }
    }
}

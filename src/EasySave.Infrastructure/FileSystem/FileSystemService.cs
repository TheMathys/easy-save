using EasySave.Core.Interfaces;
using EasySave.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace EasySave.Infrastructure.FileSystem
{
    /// <summary>
    /// Implementation of file system operations (local, external, network).
    /// </summary>
    public sealed class FileSystemService : IFileSystemService
    {
        public void EnsureDirectoryExists(string directoryPath)
        {
            Directory.CreateDirectory(directoryPath);
        }

        private static string GetUncPathWindows(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
                return fullPath; // déjà UNC

            var drive = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(drive) || drive.Length < 2)
                return fullPath;

            int length = 260;
            StringBuilder sb = new StringBuilder(length);
            if (NativeMethods.WNetGetConnectionW(drive.TrimEnd('\\'), sb, ref length) == 0)
                return fullPath.Replace(drive, sb.ToString().TrimEnd('\\') + Path.DirectorySeparatorChar);

            return fullPath;
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
                    string relativePath = Path.GetRelativePath(sourcePath, file);
                    DateTime lastWriteUtc = File.GetLastWriteTimeUtc(file);
                    yield return new FileItem(relativePath, file, lastWriteUtc);
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
            return new FileInfo(path).Length;
        }

        public DateTime GetLastWriteTimeUtc(string path)
        {
            return new FileInfo(path).LastWriteTimeUtc;
        }

        public string GetUncPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return GetUncPathWindows(path);
                return Path.GetFullPath(path);
            }
            catch
            {
                return Path.GetFullPath(path);
            }
        }
        
        /// <summary>
        /// Copy buffer size (80 KB). Trade-off between speed and memory usage to avoid overloading the machine.
        /// </summary>
        private const int CopyBufferSize = 81920;

        public async Task<long> CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                string? dir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(dir))
                    EnsureDirectoryExists(dir);

                var sourceOptions = new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.Read,
                    Options = FileOptions.Asynchronous
                };
                var destOptions = new FileStreamOptions
                {
                    Mode = FileMode.Create,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    Options = FileOptions.Asynchronous
                };

                await using (var source = new FileStream(sourcePath, sourceOptions))
                await using (var dest = new FileStream(destinationPath, destOptions))
                {
                    await source.CopyToAsync(dest, CopyBufferSize, cancellationToken).ConfigureAwait(false);
                }

                stopwatch.Stop();
                return stopwatch.ElapsedMilliseconds;
            }
            catch
            {
                stopwatch.Stop();
                return -stopwatch.ElapsedMilliseconds;
            }
        }
        private static class NativeMethods
        {
            [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern int WNetGetConnectionW(string localName, StringBuilder remoteName, ref int length);
        }
    }
}
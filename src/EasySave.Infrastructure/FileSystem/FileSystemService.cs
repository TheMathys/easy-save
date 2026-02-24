using EasySave.Core.Interfaces;
using EasySave.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Concurrent;
using System.Threading;

namespace EasySave.Infrastructure.FileSystem
{
    /// <summary>
    /// Implementation of file system operations (local, external, network).
    /// </summary>
    public sealed class FileSystemService : IFileSystemService
    {
        private readonly ConcurrentDictionary<string, string> _uncRootCache = new(StringComparer.OrdinalIgnoreCase);

        public void EnsureDirectoryExists(string directoryPath)
        {
            Directory.CreateDirectory(directoryPath);
        }

        private string GetUncPathWindows(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
                return fullPath;

            var drive = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(drive) || drive.Length < 2)
                return fullPath;

            string uncRoot = _uncRootCache.GetOrAdd(drive, ResolveUncRoot);
            if (uncRoot != drive)
                return fullPath.Replace(drive, uncRoot);

            return fullPath;
        }

        private static string ResolveUncRoot(string drive)
        {
            int length = 260;
            StringBuilder sb = new StringBuilder(length);
            if (NativeMethods.WNetGetConnectionW(drive.TrimEnd('\\'), sb, ref length) == 0)
                return sb.ToString().TrimEnd('\\') + Path.DirectorySeparatorChar;
            return drive;
        }

        public async IAsyncEnumerable<FileItem> EnumerateFilesAsync(string sourcePath, BackupEnumerationOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(sourcePath))
                yield break;

            var excludeExtensions = BuildExcludeExtensionsSet(options?.ExcludeExtensions);
            var excludeDirNames = BuildExcludeDirectoryNamesSet(options?.ExcludeDirectoryNames);

            var stack = new Stack<string>();
            stack.Push(sourcePath);

            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = stack.Pop();

                foreach (var file in Directory.EnumerateFiles(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (excludeExtensions != null)
                    {
                        var ext = Path.GetExtension(file);
                        if (!string.IsNullOrEmpty(ext) && excludeExtensions.Contains(ext))
                            continue;
                    }
                    string relativePath = Path.GetRelativePath(sourcePath, file);
                    DateTime lastWriteUtc = File.GetLastWriteTimeUtc(file);
                    yield return new FileItem(relativePath, file, lastWriteUtc);
                }

                foreach (var dir in Directory.EnumerateDirectories(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (excludeDirNames != null)
                    {
                        var dirName = Path.GetFileName(dir);
                        if (!string.IsNullOrEmpty(dirName) && excludeDirNames.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                            continue;
                    }
                    stack.Push(dir);
                }
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static HashSet<string>? BuildExcludeExtensionsSet(IReadOnlyList<string>? list)
        {
            if (list == null || list.Count == 0) return null;
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in list)
            {
                var t = (s ?? "").Trim();
                if (string.IsNullOrEmpty(t)) continue;
                if (!t.StartsWith(".", StringComparison.Ordinal)) t = "." + t;
                set.Add(t);
            }
            return set.Count == 0 ? null : set;
        }

        private static HashSet<string>? BuildExcludeDirectoryNamesSet(IReadOnlyList<string>? list)
        {
            if (list == null || list.Count == 0) return null;
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in list)
            {
                var t = (s ?? "").Trim();
                if (!string.IsNullOrEmpty(t)) set.Add(t);
            }
            return set.Count == 0 ? null : set;
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
        /// Copy buffer size (1 MB). Trade-off between speed and memory usage to avoid overloading the machine.
        /// </summary>
        private const int CopyBufferSize = 1024 * 1024;

        /// <summary>
        /// Report progress at most every this many bytes to limit callback overhead during copy (e.g. 202 MB = ~202 callbacks instead of ~2600).
        /// </summary>
        private const long ProgressReportIntervalBytes = CopyBufferSize;

        public async Task<long> CopyFileAsync(string sourcePath, string destinationPath, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
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
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan
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
                    long totalCopied = 0;
                    long lastReported = -ProgressReportIntervalBytes;
                    var buffer = new byte[CopyBufferSize];
                    int read;
                    while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await dest.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        totalCopied += read;
                        if (progress != null && totalCopied - lastReported >= ProgressReportIntervalBytes)
                        {
                            lastReported = totalCopied;
                            progress.Report(totalCopied);
                        }
                    }
                    if (progress != null && totalCopied > 0 && lastReported != totalCopied)
                        progress.Report(totalCopied);
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
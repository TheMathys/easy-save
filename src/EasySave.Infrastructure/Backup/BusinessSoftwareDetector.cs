using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EasySave.Core.Interfaces;

namespace EasySave.Infrastructure.Backup
{
    /// <summary>
    /// Detects if a process with the given name is running (e.g. Calculator for demos).
    /// Matches by process name (case-insensitive); ".exe" suffix in config is stripped for comparison.
    /// </summary>
    public sealed class BusinessSoftwareDetector : IBusinessSoftwareDetector
    {
        /// <inheritdoc />
        public bool IsRunning(string? processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return false;

            string name = processName.Trim();
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = name[..^4];

            try
            {
                foreach (Process process in Process.GetProcesses())
                {
                    try
                    {
                        if (string.Equals(process.ProcessName, name, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch
            {
                // Ignore (e.g. access denied on some processes)
            }

            return false;
        }

        /// <inheritdoc />
        public IReadOnlyList<string> GetRunningProcessNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (Process process in Process.GetProcesses())
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(process.ProcessName))
                            names.Add(process.ProcessName);
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch
            {
                // Ignore (e.g. access denied)
            }

            return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}

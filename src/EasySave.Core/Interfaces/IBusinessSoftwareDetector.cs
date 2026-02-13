using System.Collections.Generic;

namespace EasySave.Core.Interfaces
{
    /// <summary>
    /// Detects whether a configured "business software" process is currently running.
    /// Used to block backup start and to stop after the current file during backup.
    /// </summary>
    public interface IBusinessSoftwareDetector
    {
        /// <summary>
        /// Returns true if a process matching the given name is running.
        /// </summary>
        /// <param name="processName">Process name (e.g. "Calculator" for Calculator.exe). Null or empty returns false.</param>
        bool IsRunning(string? processName);

        /// <summary>
        /// Returns the names of currently running processes (without .exe), for UI selection.
        /// </summary>
        IReadOnlyList<string> GetRunningProcessNames();
    }
}

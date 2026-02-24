namespace EasySave.Core.Enums;

/// <summary>
/// Where backup log entries are written: local files only, centralized server only, or both.
/// </summary>
public enum LogDestination
{
    /// <summary>
    /// Logs are written only to local daily files (current behavior).
    /// </summary>
    Local = 0,

    /// <summary>
    /// Logs are sent only to the centralized log server (no local files).
    /// </summary>
    Centralized = 1,

    /// <summary>
    /// Logs are written both locally and sent to the centralized server.
    /// </summary>
    LocalAndCentralized = 2
}

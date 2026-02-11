namespace EasySave.Core.Enums
{
    /// <summary>
    /// Represents the backup log file format.
    /// </summary>
    public enum LogFileFormat
    {
        /// <summary>
        /// Logs written in JSON format (default, yyyy-MM-dd.json).
        /// </summary>
        Json = 0,

        /// <summary>
        /// Logs written in XML format (file yyyy-MM-dd.xml).
        /// </summary>
        Xml = 1
    }
}


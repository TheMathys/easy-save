using System;

namespace EasySave.Core.Exceptions
{
    /// <summary>
    /// Thrown when backup cannot start because the configured business software process is detected as running.
    /// </summary>
    public sealed class BusinessSoftwareDetectedException : InvalidOperationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessSoftwareDetectedException"/> class.
        /// </summary>
        public BusinessSoftwareDetectedException()
            : base("Business software is running. Backup cannot start.")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessSoftwareDetectedException"/> class with a custom message.
        /// </summary>
        public BusinessSoftwareDetectedException(string message)
            : base(message)
        {
        }
    }
}

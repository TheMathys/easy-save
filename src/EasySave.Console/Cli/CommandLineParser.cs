using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace EasySave.Console.Cli
{

    /// <summary>
    /// Parses the command-line arguments to determine which backup jobs must be executed.
    /// The <see cref="Parse"/> method accepts raw arguments and returns a list of positive job identifiers.
    /// Invalid or out-of-range identifiers are excluded from the result.
    /// </summary>
    public static class CommandLineParser
    {
        /// <summary>
        /// Indicates whether the application should start the TUI (no arguments or first argument is --tui).
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>
        /// True to start the TUI, false to run in CLI mode (e.g. EasySave 1-3).
        /// </returns>
        public static bool ShouldRunTui(string[]? args)
        {
            if (args == null || args.Length == 0)
                return true;
        
            return string.Equals(args[0], "--tui", StringComparison.OrdinalIgnoreCase);
        }

        public static IReadOnlyList<int> Parse(string[] args)
        {
            if (args == null || args.Length == 0)
                return Array.Empty<int>();

            string raw = string.Join(" ", args).Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<int>();

            List<int> result = new List<int>();

            // Support "1-3" (range) and "1;3" or "1,3" (list)
            if (raw.Contains('-') && !raw.Contains(';') && !raw.Contains(','))
            {
                string[] parts = raw.Split('-');
                if (parts.Length == 2 &&
                    int.TryParse(parts[0].Trim(), out int start) &&
                    int.TryParse(parts[1].Trim(), out int end))
                {
                    for (int i = Math.Min(start, end); i <= Math.Max(start, end); i++)
                        if (i >= 1)
                            result.Add(i);
                    return result;
                }
            }

            char[] separators = new[] { ';', ',' };
            foreach (string token in raw.Split(separators, StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(token.Trim(), out int id) && id >= 1 && !result.Contains(id))
                    result.Add(id);
            }

            return result;
        }
    }
}

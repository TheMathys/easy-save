using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace EasySave.Console.Cli
{

    /// <summary>
    /// Parses the command-line arguments to determine which backup jobs must be executed.
    /// The <see cref="Parse"/> method accepts raw arguments and returns a list of job identifiers between 1 and 5.
    /// Invalid or out-of-range identifiers are excluded from the result.
    /// </summary>
    public static class CommandLineParser
    {
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
                        if (i >= 1 && i <= 5)
                            result.Add(i);
                    return result;
                }
            }

            char[] separators = new[] { ';', ',' };
            foreach (string token in raw.Split(separators, StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(token.Trim(), out int id) && id >= 1 && id <= 5 && !result.Contains(id))
                    result.Add(id);
            }

            return result;
        }
    }
}

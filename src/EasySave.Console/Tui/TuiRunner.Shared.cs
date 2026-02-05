using System;
using System.Collections.Generic;
using System.Linq;

namespace EasySave.Console.Tui
{
    /// <summary>Shared helpers for path and list input.</summary>
    public static partial class TuiRunner
    {
        private static List<string> ParseCommaSeparatedList(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return new List<string>();
            return input!.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        /// <summary>
        /// Reads a path line. Uses Tab completion when console is interactive; otherwise uses ReadLine.
        /// </summary>
        private static string? ReadPathLine(string? prompt, string fallbackPrompt)
        {
            System.Console.Write($"{prompt ?? fallbackPrompt}: ");
            if (!System.Console.IsInputRedirected)
            {
                try
                {
                    return PathCompletionLineReader.ReadPathWithTabCompletion()?.Trim();
                }
                catch (InvalidOperationException)
                {
                    // ReadKey not available (e.g. no console)
                }
            }
            return System.Console.ReadLine()?.Trim();
        }
    }
}

using System;
using System.Collections.Generic;

namespace EasySave.Gui.Cli;

/// <summary>
/// Parses command-line arguments for the GUI in the same format as the Console:
/// - Range: 1-3 or 1~3
/// - List: 1;3 or 1,3,5 (separators ';' or ',')
/// - Combined: 1~3;5 or 1-3,5
/// Example: EasySave.Gui.exe 1~3 or EasySave.Gui.exe 1;3;5
/// </summary>
public static class GuiCommandLineParser
{
    /// <summary>
    /// Parses the command-line arguments and returns the list of backup job IDs to run.
    /// Returns an empty list if no valid job IDs are found (e.g. empty args or --tui).
    /// </summary>
    /// <param name="args">Command-line arguments (e.g. from Main).</param>
    /// <returns>List of job IDs to execute (1-based).</returns>
    public static IReadOnlyList<int> Parse(string[]? args)
    {
        if (args == null || args.Length == 0)
            return Array.Empty<int>();

        // Ignore --tui so that "EasySave.Gui.exe --tui" just opens the GUI with no auto-run
        if (string.Equals(args[0], "--tui", StringComparison.OrdinalIgnoreCase))
            return Array.Empty<int>();

        string raw = string.Join(" ", args).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<int>();

        List<int> result = new List<int>();
        char[] listSeparators = new[] { ';', ',' };

        // Same logic as Console TUI (TuiRunner.RunJobs): split by ; and , then each part is either a range (1-3 or 1~3) or a single id
        foreach (string part in raw.Split(listSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = part.Trim();
            if (trimmed.Contains('-') || trimmed.Contains('~'))
            {
                char rangeChar = trimmed.Contains('~') ? '~' : '-';
                string[] rangeParts = trimmed.Split(rangeChar);
                if (rangeParts.Length == 2 &&
                    int.TryParse(rangeParts[0].Trim(), out int start) &&
                    int.TryParse(rangeParts[1].Trim(), out int end))
                {
                    for (int i = Math.Min(start, end); i <= Math.Max(start, end); i++)
                        if (i >= 1 && !result.Contains(i))
                            result.Add(i);
                }
            }
            else if (int.TryParse(trimmed, out int id) && id >= 1 && !result.Contains(id))
            {
                result.Add(id);
            }
        }

        return result;
    }
}

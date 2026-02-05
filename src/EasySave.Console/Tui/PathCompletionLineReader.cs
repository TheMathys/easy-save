using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace EasySave.Console.Tui
{
    /// <summary>
    /// Reads a single line with Tab completion for paths (drives then subdirectories).
    /// Behaves like a shell: one match → full path + separator; multiple matches → common prefix.
    /// Case-insensitive on Windows. The line remains fully editable (backspace, delete, cursor, etc.).
    /// </summary>
    public static class PathCompletionLineReader
    {
        /// <summary>
        /// Reads a path from the console with Tab completion. Use only when console input is not redirected.
        /// When input is redirected, use <see cref="System.Console.ReadLine"/> instead.
        /// </summary>
        public static string? ReadPathWithTabCompletion()
        {
            int startLeft = System.Console.CursorLeft;
            int startTop = System.Console.CursorTop;
            var buffer = new StringBuilder();
            int cursor = 0;
            int lastDrawnLength = 0;

            void Redraw()
            {
                System.Console.SetCursorPosition(startLeft, startTop);
                string s = buffer.ToString();
                System.Console.Write(s);
                int clearCount = lastDrawnLength - s.Length;
                if (clearCount > 0)
                {
                    System.Console.Write(new string(' ', clearCount));
                }
                lastDrawnLength = s.Length;
                System.Console.SetCursorPosition(startLeft + cursor, startTop);
            }

            while (true)
            {
                ConsoleKeyInfo key = System.Console.ReadKey(intercept: true);

                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        System.Console.SetCursorPosition(startLeft + buffer.Length, startTop);
                        System.Console.WriteLine();
                        return buffer.ToString();
                    case ConsoleKey.Tab:
                        string current = buffer.ToString();
                        IReadOnlyList<string> completions = GetPathCompletions(current);
                        if (completions.Count == 0)
                        {
                            System.Console.Beep();
                            break;
                        }
                        if (completions.Count == 1)
                        {
                            buffer.Clear();
                            buffer.Append(completions[0]);
                            if (!completions[0].EndsWith(Path.DirectorySeparatorChar) && !completions[0].EndsWith(Path.AltDirectorySeparatorChar))
                                buffer.Append(Path.DirectorySeparatorChar);
                            cursor = buffer.Length;
                        }
                        else
                        {
                            string common = GetCommonPrefix(completions);
                            if (common.Length > current.Length)
                            {
                                buffer.Clear();
                                buffer.Append(common);
                                cursor = buffer.Length;
                            }
                            else
                            {
                                System.Console.Beep();
                            }
                        }
                        Redraw();
                        break;
                    case ConsoleKey.Backspace:
                        if (cursor > 0)
                        {
                            buffer.Remove(cursor - 1, 1);
                            cursor--;
                            Redraw();
                        }
                        break;
                    case ConsoleKey.Delete:
                        if (cursor < buffer.Length)
                        {
                            buffer.Remove(cursor, 1);
                            Redraw();
                        }
                        break;
                    case ConsoleKey.LeftArrow:
                        if (cursor > 0)
                        {
                            cursor--;
                            System.Console.SetCursorPosition(startLeft + cursor, startTop);
                        }
                        break;
                    case ConsoleKey.RightArrow:
                        if (cursor < buffer.Length)
                        {
                            cursor++;
                            System.Console.SetCursorPosition(startLeft + cursor, startTop);
                        }
                        break;
                    case ConsoleKey.Home:
                        cursor = 0;
                        System.Console.SetCursorPosition(startLeft, startTop);
                        break;
                    case ConsoleKey.End:
                        cursor = buffer.Length;
                        System.Console.SetCursorPosition(startLeft + cursor, startTop);
                        break;
                    default:
                        if (key.KeyChar >= ' ')
                        {
                            buffer.Insert(cursor, key.KeyChar);
                            cursor++;
                            Redraw();
                        }
                        break;
                }
            }
        }

        private static IReadOnlyList<string> GetPathCompletions(string path)
        {
            string p = (path ?? "").Trim().Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            var comparer = StringComparer.OrdinalIgnoreCase;

            // Empty: list drives (Windows) or "/" (Unix)
            if (string.IsNullOrWhiteSpace(p))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        return Directory.GetLogicalDrives();
                    }
                    catch
                    {
                        return Array.Empty<string>();
                    }
                }
                return new[] { Path.DirectorySeparatorChar.ToString() };
            }

            p = p.TrimEnd(Path.DirectorySeparatorChar);
            string? parent = Path.GetDirectoryName(p);
            string prefix = Path.GetFileName(p) ?? "";

            // "C:" or "C" on Windows → complete to matching drive(s)
            if (string.IsNullOrEmpty(parent) && RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && p.Length <= 2)
            {
                try
                {
                    string driveLetter = p.TrimEnd(':').ToLowerInvariant();
                    if (driveLetter.Length == 0) return Array.Empty<string>();
                    var drives = Directory.GetLogicalDrives();
                    var list = new List<string>();
                    foreach (string d in drives)
                    {
                        string dl = d.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();
                        if (dl.StartsWith(driveLetter, StringComparison.Ordinal))
                            list.Add(d);
                    }
                    return list;
                }
                catch
                {
                    return Array.Empty<string>();
                }
            }

            string searchRoot = string.IsNullOrEmpty(parent) ? p + Path.DirectorySeparatorChar : parent + Path.DirectorySeparatorChar;
            if (string.IsNullOrEmpty(parent))
                prefix = "";

            try
            {
                if (!Directory.Exists(searchRoot.TrimEnd(Path.DirectorySeparatorChar)))
                    return Array.Empty<string>();

                string[] dirs = Directory.GetDirectories(searchRoot);
                var list = new List<string>();
                foreach (string d in dirs)
                {
                    string name = Path.GetFileName(d.TrimEnd(Path.DirectorySeparatorChar)) ?? "";
                    if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        list.Add(d.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
                }
                list.Sort(comparer);
                return list;
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static string GetCommonPrefix(IReadOnlyList<string> paths)
        {
            if (paths.Count == 0) return "";
            if (paths.Count == 1) return paths[0];

            string first = paths[0];
            int len = first.Length;
            for (int i = 1; i < paths.Count; i++)
            {
                string s = paths[i];
                int j = 0;
                while (j < len && j < s.Length && char.ToLowerInvariant(first[j]) == char.ToLowerInvariant(s[j]))
                    j++;
                len = j;
            }
            return first.Substring(0, len);
        }
    }
}

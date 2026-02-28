using System;
using EasySave.Console.Resources;
using EasySave.Core.Entities;

namespace EasySave.Console
{
    /// <summary>
    /// Renders a single progress line (bar, percent, size, ETA) for console output.
    /// </summary>
    public static class ProgressDisplay
    {
        private const int BarLength = 24;

        /// <summary>
        /// Writes one progress line to the console (overwrites current line with \r).
        /// No-op when there is no console (e.g. in tests).
        /// </summary>
        public static void WriteProgressLine(BackupProgress p)
        {
            if (p == null) return;
            if (!TryGetConsoleWidth(out int width)) return;

            long total = p.TotalSizeBytes;
            long completed = total > 0 ? total - p.RemainingSizeBytes : 0;
            double percent = p.ProgressPercent;
            string bar = BuildBar(percent);
            string sizeStr = FormatSize(completed) + " / " + FormatSize(total);
            string etaStr = FormatEta(p.EstimatedTimeRemainingSeconds);
            string? etaFormat = LangHelper.GetString("ETAFormat");
            string etaLine = string.Format(etaFormat ?? "ETA: {0}", etaStr);

            string namePrefix = string.IsNullOrWhiteSpace(p.BackupName) ? string.Empty : p.BackupName + " ";
            string line = namePrefix + $"{bar} {percent:F1}% {sizeStr} {etaLine}";
            if (line.Length < width)
                line = line + new string(' ', width - line.Length);
            else if (line.Length > width)
                line = line.Substring(0, width);

            try { System.Console.Write("\r" + line); } catch (System.IO.IOException) { }
        }

        /// <summary>
        /// Clears the progress line (e.g. after completion) so the next write starts clean.
        /// No-op when there is no console (e.g. in tests).
        /// </summary>
        public static void ClearProgressLine()
        {
            if (!TryGetConsoleWidth(out int width)) return;
            try { System.Console.Write("\r" + new string(' ', width) + "\r"); } catch (System.IO.IOException) { }
        }

        private static bool TryGetConsoleWidth(out int width)
        {
            width = 80;
            try
            {
                width = System.Console.BufferWidth;
                return true;
            }
            catch (System.IO.IOException)
            {
                return false;
            }
        }

        private static string BuildBar(double percent)
        {
            int filled = (int)Math.Round(BarLength * Math.Clamp(percent, 0, 100) / 100.0);
            int empty = BarLength - filled;
            return "[" + new string('=', filled) + new string(' ', empty) + "]";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 0) return "0 B";
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int u = 0;
            double v = bytes;
            while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
            return u == 0 ? $"{v:F0} {units[u]}" : $"{v:F2} {units[u]}";
        }

        private static string FormatEta(double? seconds)
        {
            if (seconds == null || double.IsNaN(seconds.Value) || seconds.Value < 0)
                return LangHelper.GetString("ETANone") ?? "--";

            double s = seconds.Value;
            if (s >= 60)
            {
                int min = (int)(s / 60);
                int sec = (int)Math.Round(s % 60);
                string? fmt = LangHelper.GetString("ETAMinSec");
                return fmt != null ? string.Format(fmt, min, sec) : $"{min} min {sec} s";
            }
            string? secFmt = LangHelper.GetString("ETASecondsOnly");
            return secFmt != null ? string.Format(secFmt, (int)Math.Round(s)) : $"{(int)Math.Round(s)} s";
        }
    }
}

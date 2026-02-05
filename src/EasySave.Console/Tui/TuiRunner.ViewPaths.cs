using System;
using EasySave.Console.Resources;

namespace EasySave.Console.Tui
{
    /// <summary>Option "Voir les chemins (config et logs)".</summary>
    public static partial class TuiRunner
    {
        private static void ViewPaths(EasySavePaths paths)
        {
            System.Console.WriteLine();
            string? configLabel = LangHelper.GetString("TuiViewPathsConfig");
            System.Console.WriteLine(configLabel ?? "Config, state and log directory:");
            System.Console.WriteLine(paths.BaseDirectory);
            System.Console.WriteLine();
            string? hint = LangHelper.GetString("TuiViewPathsHint");
            System.Console.WriteLine(hint ?? "backup-config.json, state.json and daily log files (yyyy-MM-dd.json) are stored in this directory.");
        }
    }
}

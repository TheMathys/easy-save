using System;
using System.Collections.Generic;
using Avalonia;
using EasySave.Gui.Cli;

namespace EasySave.Gui;

/// <summary>
/// Application entry point for the EasySave GUI.
/// Responsible for building the DI container and bootstrapping Avalonia.
/// Supports CLI launch with job IDs (same format as Console): e.g. EasySave.Gui.exe 1 2 3 or 1-3 or 1,3,5
/// </summary>
internal sealed class Program
{
    [STAThread]
    /// <summary>
    /// Main entry method for the GUI process.
    /// </summary>
    /// <param name="args">Command-line arguments (e.g. job IDs to run at startup).</param>
    public static void Main(string[] args)
    {
        string basePath = GuiBasePathStorage.GetBasePath();
        App.ServiceProvider = CompositionRoot.Build(basePath);

        IReadOnlyList<int> pendingJobIds = GuiCommandLineParser.Parse(args);
        if (pendingJobIds.Count > 0)
            App.PendingJobIdsToRun = pendingJobIds;

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Configures the Avalonia <see cref="AppBuilder"/> for the GUI.
    /// </summary>
    /// <returns>The configured <see cref="AppBuilder"/> instance.</returns>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}

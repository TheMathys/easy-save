using System;
using Avalonia;

namespace EasySave.Gui;

/// <summary>
/// Application entry point for the EasySave GUI.
/// Responsible for building the DI container and bootstrapping Avalonia.
/// </summary>
internal sealed class Program
{
    [STAThread]
    /// <summary>
    /// Main entry method for the GUI process.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    public static void Main(string[] args)
    {
        string? envBasePath = Environment.GetEnvironmentVariable("EASYSAVE_BASE_PATH");
        string basePath = !string.IsNullOrWhiteSpace(envBasePath) ? envBasePath : AppContext.BaseDirectory;
        App.ServiceProvider = CompositionRoot.Build(basePath);

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

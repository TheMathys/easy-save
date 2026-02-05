using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace EasySave.Console.Tests;

public sealed class ProgramTests
{
    private static string SolutionRoot => FindSolutionRoot();

    private static string ConsoleCsprojPath =>
        Path.Combine(SolutionRoot, "src", "EasySave.Console", "EasySave.Console.csproj");

    [Fact]
    public async Task Program_UsesEnvBasePath_WhenDefined()
    {
        string tempBasePath = Path.Combine(Path.GetTempPath(), "EasySave.Console.ExecTests", Guid.NewGuid().ToString());

        ProcessStartInfo psi = new()
        {
            FileName = "dotnet",
            Arguments = $"run --configuration Release --project \"{ConsoleCsprojPath}\"",
            WorkingDirectory = SolutionRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.Environment["EASYSAVE_BASE_PATH"] = tempBasePath;

        using Process process = Process.Start(psi)!;
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"Process exited with {process.ExitCode}, stderr: {error}");
        Assert.Contains(tempBasePath, output);
    }

    [Fact]
    public async Task Program_Runs_WhenEnvBasePathIsNotDefined()
    {
        ProcessStartInfo psi = new()
        {
            FileName = "dotnet",
            Arguments = $"run --configuration Release --project \"{ConsoleCsprojPath}\"",
            WorkingDirectory = SolutionRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.Environment.Remove("EASYSAVE_BASE_PATH");

        using Process process = Process.Start(psi)!;
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"Process exited with {process.ExitCode}, stderr: {error}");
        Assert.Contains("EasySave console initialized with base path:", output);
    }

    private static string FindSolutionRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (true)
        {
            string candidate = Path.Combine(dir, "EasySave.sln");
            if (File.Exists(candidate))
                return dir;

            string? parent = Path.GetDirectoryName(dir);
            if (string.IsNullOrEmpty(parent) || parent == dir)
                throw new InvalidOperationException("Could not locate solution root (EasySave.sln).");

            dir = parent;
        }
    }
}


using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Console.Tui;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EasySave.Console.Tests;

public sealed class TuiRunnerTests : IDisposable
{
    private readonly TextWriter _originalOut;
    private readonly TextReader _originalIn;

    public TuiRunnerTests()
    {
        _originalOut = System.Console.Out;
        _originalIn = System.Console.In;
    }

    public void Dispose()
    {
        System.Console.SetOut(_originalOut);
        System.Console.SetIn(_originalIn);
    }

    private static (IServiceProvider Provider, FakeConfigRepository ConfigRepository, FakeBackupExecutor BackupExecutor)
        CreateProvider(BackupConfiguration? initialConfig = null)
    {
        FakeConfigRepository configRepository = new(initialConfig);
        FakeBackupExecutor backupExecutor = new();

        ServiceCollection services = new();
        services.AddSingleton<IConfigurationRepository>(configRepository);
        services.AddSingleton<IBackupExecutor>(backupExecutor);

        IServiceProvider provider = services.BuildServiceProvider();
        return (provider, configRepository, backupExecutor);
    }

    [Fact]
    public async Task RunAsync_DisplaysMenu_AndQuits_OnZeroChoice()
    {
        string input = "0" + Environment.NewLine;
        StringWriter output = new();
        System.Console.SetIn(new StringReader(input));
        System.Console.SetOut(output);

        BackupConfiguration initialConfig = new()
        {
            LogAndStateDirectory = string.Empty,
            Jobs = Array.Empty<BackupJob>(),
            LastFullBackupUtcByJobId = new Dictionary<int, DateTime>()
        };

        (IServiceProvider provider, _, _) = CreateProvider(initialConfig);

        await TuiRunner.RunAsync(provider);

        string text = output.ToString();
        Assert.Contains("EasySave", text);              // titre du menu (EN ou FR)
        Assert.Contains("1.", text);                    // option 1
        Assert.Contains("0.", text);                    // option 0
    }

    [Fact]
    public async Task RunAsync_ShowsError_OnInvalidChoice()
    {
        string input = "x" + Environment.NewLine + "0" + Environment.NewLine;
        StringWriter output = new();
        System.Console.SetIn(new StringReader(input));
        System.Console.SetOut(output);

        BackupConfiguration initialConfig = new()
        {
            LogAndStateDirectory = string.Empty,
            Jobs = Array.Empty<BackupJob>(),
            LastFullBackupUtcByJobId = new Dictionary<int, DateTime>()
        };

        (IServiceProvider provider, _, _) = CreateProvider(initialConfig);

        await TuiRunner.RunAsync(provider);

        string text = output.ToString();
        Assert.Contains("invalid", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_CreatesJob_WhenOption1Selected()
    {
        string input =
            "1" + Environment.NewLine +     // menu: create job
            "Job 1" + Environment.NewLine + // nom
            "C:\\Source" + Environment.NewLine +
            "D:\\Target" + Environment.NewLine +
            "1" + Environment.NewLine +     // type Full
            "0" + Environment.NewLine;      // retour menu puis quitter

        StringWriter output = new();
        System.Console.SetIn(new StringReader(input));
        System.Console.SetOut(output);

        (IServiceProvider provider, FakeConfigRepository repo, _) = CreateProvider(null);

        await TuiRunner.RunAsync(provider);

        BackupConfiguration? config = await repo.LoadAsync(CancellationToken.None);
        Assert.NotNull(config);
        Assert.Single(config!.Jobs);

        BackupJob job = config.Jobs[0];
        Assert.Equal(1, job.Id);
        Assert.Equal("Job 1", job.Name);
        Assert.Equal("C:\\Source", job.SourcePath);
        Assert.Equal("D:\\Target", job.TargetPath);
        Assert.Equal(BackupType.Full, job.Type);

        string text = output.ToString();
        Assert.Contains("Job 1", text);
    }

    [Fact]
    public async Task RunAsync_ListsJobs_WhenOption2Selected()
    {
        BackupConfiguration initialConfig = new()
        {
            LogAndStateDirectory = string.Empty,
            Jobs = new List<BackupJob>
            {
                new() { Id = 1, Name = "Job A", SourcePath = "S1", TargetPath = "T1", Type = BackupType.Full },
                new() { Id = 2, Name = "Job B", SourcePath = "S2", TargetPath = "T2", Type = BackupType.Differential }
            },
            LastFullBackupUtcByJobId = new Dictionary<int, DateTime>()
        };

        string input = "2" + Environment.NewLine + "0" + Environment.NewLine;
        StringWriter output = new();
        System.Console.SetIn(new StringReader(input));
        System.Console.SetOut(output);

        (IServiceProvider provider, _, _) = CreateProvider(initialConfig);

        await TuiRunner.RunAsync(provider);

        string text = output.ToString();
        Assert.Contains("Job 1", text);
        Assert.Contains("Job A", text);
        Assert.Contains("Job 2", text);
        Assert.Contains("Job B", text);
    }

    [Fact]
    public async Task RunAsync_RunsJobs_WhenOption3Selected()
    {
        BackupConfiguration initialConfig = new()
        {
            LogAndStateDirectory = string.Empty,
            Jobs = new List<BackupJob>
            {
                new() { Id = 1, Name = "Job A", SourcePath = "S1", TargetPath = "T1", Type = BackupType.Full },
                new() { Id = 2, Name = "Job B", SourcePath = "S2", TargetPath = "T2", Type = BackupType.Differential }
            },
            LastFullBackupUtcByJobId = new Dictionary<int, DateTime>()
        };

        string input =
            "3" + Environment.NewLine + // exécuter sauvegardes
            "1,2" + Environment.NewLine +
            "0" + Environment.NewLine;

        StringWriter output = new();
        System.Console.SetIn(new StringReader(input));
        System.Console.SetOut(output);

        (IServiceProvider provider, _, FakeBackupExecutor executor) = CreateProvider(initialConfig);

        await TuiRunner.RunAsync(provider);

        Assert.Single(executor.Executions);
        IReadOnlyList<int> executed = executor.Executions[0];
        Assert.Equal(new List<int> { 1, 2 }, executed);

        string text = output.ToString();
        Assert.Contains("Starting backup", text);
    }

    [Fact]
    public async Task RunAsync_ShowsHelp_WhenOption4Selected()
    {
        string input = "4" + Environment.NewLine + "0" + Environment.NewLine;
        StringWriter output = new();
        System.Console.SetIn(new StringReader(input));
        System.Console.SetOut(output);

        BackupConfiguration initialConfig = new()
        {
            LogAndStateDirectory = string.Empty,
            Jobs = Array.Empty<BackupJob>(),
            LastFullBackupUtcByJobId = new Dictionary<int, DateTime>()
        };

        (IServiceProvider provider, _, _) = CreateProvider(initialConfig);

        await TuiRunner.RunAsync(provider);

        string text = output.ToString();
        Assert.Contains("Help", text, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeConfigRepository : IConfigurationRepository
    {
        private BackupConfiguration? _configuration;

        public FakeConfigRepository(BackupConfiguration? initialConfig)
        {
            _configuration = initialConfig;
        }

        public Task<BackupConfiguration?> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_configuration);
        }

        public Task SaveAsync(BackupConfiguration backupConfiguration, CancellationToken cancellationToken)
        {
            _configuration = backupConfiguration;
            return Task.CompletedTask;
        }

        public Task UpdateLastFullBackupAsync(int jobId, DateTime utc, CancellationToken cancellationToken)
        {
            if (_configuration == null)
            {
                _configuration = new BackupConfiguration
                {
                    LogAndStateDirectory = string.Empty,
                    Jobs = Array.Empty<BackupJob>(),
                    LastFullBackupUtcByJobId = new Dictionary<int, DateTime>()
                };
            }

            Dictionary<int, DateTime> map = new(_configuration.LastFullBackupUtcByJobId)
            {
                [jobId] = utc
            };

            _configuration = new BackupConfiguration
            {
                LogAndStateDirectory = _configuration.LogAndStateDirectory,
                Jobs = _configuration.Jobs,
                LastFullBackupUtcByJobId = map
            };

            return Task.CompletedTask;
        }
    }

    private sealed class FakeBackupExecutor : IBackupExecutor
    {
        public List<IReadOnlyList<int>> Executions { get; } = new();

        public Task ExecuteAsync(IReadOnlyList<int> jobIds, CancellationToken cancellationToken = default)
        {
            Executions.Add(new List<int>(jobIds));
            return Task.CompletedTask;
        }
    }
}


using EasySave.Console.Resources;
using EasySave.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace EasySave.Console.Tui
{
    /// <summary>
    /// Runs the Text User Interface (TUI) menu loop for EasySave.
    /// </summary>
    public static partial class TuiRunner
    {
        /// <summary>
        /// Runs the TUI menu loop, resolving required services from the provided service provider.
        /// </summary>
        public static Task RunAsync(IServiceProvider provider)
        {
            return RunAsync(provider, enablePause: true);
        }

        /// <summary>
        /// Runs the TUI menu loop with an option to disable the \"press any key\" pause (useful for tests).
        /// </summary>
        public static async Task RunAsync(IServiceProvider provider, bool enablePause)
        {
            IConfigurationRepository configRepository = provider.GetRequiredService<IConfigurationRepository>();
            IBackupExecutor backupExecutor = provider.GetRequiredService<IBackupExecutor>();
            EasySavePaths paths = provider.GetRequiredService<EasySavePaths>();

            bool running = true;
            while (running)
            {
                int selectedIndex = ShowMenuAndReadChoice();

                switch (selectedIndex)
                {
                    case 0:
                        await CreateJobAsync(configRepository);
                        break;
                    case 1:
                        await ListJobsAsync(configRepository);
                        break;
                    case 2:
                        await RunJobsAsync(configRepository, backupExecutor);
                        break;
                    case 3:
                        await DeleteJobAsync(configRepository);
                        break;
                    case 4:
                        await EditJobAsync(configRepository);
                        break;
                    case 5:
                        await ViewPathsAsync(configRepository, paths);
                        break;
                    case 6:
                        ShowHelp();
                        break;
                    case 7:
                        running = false;
                        break;
                }

                if (running && enablePause)
                {
                    System.Console.WriteLine();
                    System.Console.WriteLine(LangHelper.GetString("PressKeyContinue"));
                    System.Console.ReadKey();
                    System.Console.Clear();
                }
            }
        }
    }
}

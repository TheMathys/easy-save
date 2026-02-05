using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Console.Resources;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace EasySave.Console.Tui
{
    /// <summary>
    /// Runs the Text User Interface (TUI) menu loop for EasySave.
    /// </summary>
    public static class TuiRunner
    {
        /// <summary>
        /// Runs the TUI menu loop, resolving required services from the provided service provider.
        /// Shows the \"press any key\" pause between iterations.
        /// </summary>
        /// <param name="provider">Service provider to resolve IConfigurationRepository and IBackupExecutor.</param>
        public static Task RunAsync(IServiceProvider provider)
        {
            return RunAsync(provider, enablePause: true);
        }

        /// <summary>
        /// Runs the TUI menu loop with an option to disable the \"press any key\" pause (useful for tests).
        /// </summary>
        /// <param name="provider">Service provider to resolve IConfigurationRepository and IBackupExecutor.</param>
        /// <param name="enablePause">If true, shows the \"press any key to continue\" prompt between iterations.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task RunAsync(IServiceProvider provider, bool enablePause)
        {
            IConfigurationRepository configRepository = provider.GetRequiredService<IConfigurationRepository>();
            IBackupExecutor backupExecutor = provider.GetRequiredService<IBackupExecutor>();

            bool running = true;
            while (running)
            {
                int selectedIndex = ShowMenuAndReadChoice();
                bool executedRunJobs = false;

                switch (selectedIndex)
                {
                    case 0: // Créer un job
                        await CreateJobAsync(configRepository);
                        break;
                    case 1: // Lister
                        await ListJobsAsync(configRepository);
                        break;
                    case 2: // Lancer
                        await RunJobsAsync(configRepository, backupExecutor);
                        executedRunJobs = true;
                        break;
                    case 3: // Aide
                        ShowHelp();
                        break;
                    case 4: // Quitter
                        running = false;
                        break;
                }

                // Évite l'impression de blocage après une sauvegarde terminée :
                // le menu revient directement sans afficher "Appuyez sur une touche".
                if (running && enablePause && !executedRunJobs)
                {
                    System.Console.WriteLine();
                    System.Console.WriteLine(LangHelper.GetString("PressKeyContinue"));
                    System.Console.ReadKey();
                    System.Console.Clear();
                }
            }
        }

        /// <summary>
        /// Affiche le menu et récupère le choix de l'utilisateur.
        /// - Si l'entrée de la console n'est pas redirigée, utilise un menu interactif
        ///   avec les flèches haut/bas, Entrée/Espace et Échap.
        /// - Si l'entrée est redirigée (cas des tests), retombe sur la saisie
        ///   classique par numéro / 'q'.
        /// Retourne un index entre 0 et 4 :
        ///   0 = Créer, 1 = Lister, 2 = Lancer, 3 = Aide, 4 = Quitter.
        /// </summary>
        private static int ShowMenuAndReadChoice()
        {
            if (!System.Console.IsInputRedirected)
            {
                return ShowInteractiveMenu();
            }

            // Mode "tests" / entrée redirigée : on garde le comportement existant
            // basé sur la saisie par numéro ou 'q'.
            while (true)
            {
                DisplayMenu();
                string? raw = System.Console.ReadLine()?.Trim().ToLowerInvariant();

                int mapped = MapTextChoiceToIndex(raw);
                if (mapped >= 0)
                    return mapped;

                string? errorMsg = LangHelper.GetString("MenuInvalidChoice");
                System.Console.WriteLine(errorMsg ?? "Invalid choice. Please select 1-4 or 0 to quit.");
            }
        }

        /// <summary>
        /// Menu interactif : l'utilisateur se déplace avec les flèches haut/bas,
        /// valide avec Entrée/Espace, et peut quitter avec Échap.
        /// On conserve aussi la possibilité de taper directement 1-4, 0 ou q.
        /// </summary>
        private static int ShowInteractiveMenu()
        {
            int selectedIndex = 0; // 0..4

            while (true)
            {
                System.Console.Clear();
                DisplayMenu(selectedIndex);

                string? hint = LangHelper.GetString("TuiNavigationHint");
                System.Console.WriteLine();
                System.Console.WriteLine(hint ?? "Use arrows to move, Enter/Space to validate, Esc to quit, or type 1-4 / 0 / q.");

                ConsoleKeyInfo keyInfo = System.Console.ReadKey(intercept: true);

                switch (keyInfo.Key)
                {
                    case ConsoleKey.UpArrow:
                        selectedIndex = (selectedIndex + 5 - 1) % 5;
                        break;
                    case ConsoleKey.DownArrow:
                        selectedIndex = (selectedIndex + 1) % 5;
                        break;
                    case ConsoleKey.Enter:
                    case ConsoleKey.Spacebar:
                        return selectedIndex;
                    case ConsoleKey.Escape:
                        return 4; // Quitter
                    default:
                        // On autorise aussi la saisie directe des chiffres / 'q'
                        char c = keyInfo.KeyChar;
                        if (c != '\0')
                        {
                            string text = c.ToString().ToLowerInvariant();
                            int mapped = MapTextChoiceToIndex(text);
                            if (mapped >= 0)
                                return mapped;
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Mappe la saisie texte ("1", "2", "3", "4", "0", "q", "quit")
        /// vers un index de menu 0..4, ou -1 si invalide.
        /// </summary>
        private static int MapTextChoiceToIndex(string? choice)
        {
            if (string.IsNullOrWhiteSpace(choice))
                return -1;

            string normalized = choice.Trim().ToLowerInvariant();
            return normalized switch
            {
                "1" => 0,
                "2" => 1,
                "3" => 2,
                "4" => 3,
                "0" => 4,
                "q" => 4,
                "quit" => 4,
                _ => -1
            };
        }

        private static void DisplayMenu()
        {
            string? title = LangHelper.GetString("MenuTitle");
            string? option1 = LangHelper.GetString("MenuOption1");
            string? option2 = LangHelper.GetString("MenuOption2");
            string? option3 = LangHelper.GetString("MenuOption3");
            string? option4 = LangHelper.GetString("MenuOption4");
            string? option0 = LangHelper.GetString("MenuOption0");
            string? prompt = LangHelper.GetString("MenuPrompt");

            System.Console.WriteLine(title ?? "=== EasySave Menu ===");
            System.Console.WriteLine();
            System.Console.WriteLine($"1. {option1 ?? "Create a backup job"}");
            System.Console.WriteLine($"2. {option2 ?? "List backup jobs"}");
            System.Console.WriteLine($"3. {option3 ?? "Run backups"}");
            System.Console.WriteLine($"4. {option4 ?? "Help"}");
            System.Console.WriteLine($"0. {option0 ?? "Quit"}");
            System.Console.WriteLine();
            System.Console.Write(prompt ?? "Enter your choice: ");
        }

        /// <summary>
        /// Version du menu qui affiche un curseur visuel ">" devant
        /// l'option sélectionnée pour la navigation interactive.
        /// </summary>
        private static void DisplayMenu(int selectedIndex)
        {
            string? title = LangHelper.GetString("MenuTitle");
            string? option1 = LangHelper.GetString("MenuOption1");
            string? option2 = LangHelper.GetString("MenuOption2");
            string? option3 = LangHelper.GetString("MenuOption3");
            string? option4 = LangHelper.GetString("MenuOption4");
            string? option0 = LangHelper.GetString("MenuOption0");

            System.Console.WriteLine(title ?? "=== EasySave Menu ===");
            System.Console.WriteLine();

            WriteMenuLine(0, selectedIndex, "1", option1 ?? "Create a backup job");
            WriteMenuLine(1, selectedIndex, "2", option2 ?? "List backup jobs");
            WriteMenuLine(2, selectedIndex, "3", option3 ?? "Run backups");
            WriteMenuLine(3, selectedIndex, "4", option4 ?? "Help");
            WriteMenuLine(4, selectedIndex, "0", option0 ?? "Quit");
        }

        private static void WriteMenuLine(int index, int selectedIndex, string number, string text)
        {
            string prefix = index == selectedIndex ? ">" : " ";
            System.Console.WriteLine($"{prefix} {number}. {text}");
        }

        /// <summary>
        /// Lit le type de sauvegarde (Full/Differential).
        /// - En mode interactif (entrée non redirigée) : menu avec flèches + Entrée/Espace.
        /// - En mode tests (entrée redirigée) : saisie classique "1"/"2" comme avant.
        /// </summary>
        private static BackupType ReadBackupType()
        {
            if (!System.Console.IsInputRedirected)
            {
                return ShowBackupTypeInteractiveMenu();
            }

            while (true)
            {
                string? typePrompt = LangHelper.GetString("BackupTypeSelect");
                System.Console.WriteLine();
                System.Console.WriteLine(typePrompt ?? "Select backup type (1=Full, 2=Differential):");
                System.Console.WriteLine("1. " + (LangHelper.GetString("FullBackup") ?? "Full"));
                System.Console.WriteLine("2. " + (LangHelper.GetString("DifferentialBackup") ?? "Differential"));
                System.Console.Write("> ");

                string? typeInput = System.Console.ReadLine()?.Trim();
                if (TryParseBackupType(typeInput, out BackupType backupType))
                    return backupType;

                string? invalidInputMsg = LangHelper.GetString("InvalidInput");
                System.Console.WriteLine(invalidInputMsg ?? "Invalid backup type. Please choose 1 or 2.");
            }
        }

        private static bool TryParseBackupType(string? input, out BackupType backupType)
        {
            backupType = BackupType.Full;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            string value = input.Trim();
            if (value == "1" || string.Equals(value, "full", StringComparison.OrdinalIgnoreCase))
            {
                backupType = BackupType.Full;
                return true;
            }

            if (value == "2" || string.Equals(value, "differential", StringComparison.OrdinalIgnoreCase))
            {
                backupType = BackupType.Differential;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Menu interactif pour choisir le type de sauvegarde avec les flèches.
        /// </summary>
        private static BackupType ShowBackupTypeInteractiveMenu()
        {
            int selectedIndex = 0; // 0 = Full, 1 = Differential

            while (true)
            {
                System.Console.Clear();

                string? typePrompt = LangHelper.GetString("BackupTypeSelect");
                string? full = LangHelper.GetString("FullBackup");
                string? differential = LangHelper.GetString("DifferentialBackup");

                System.Console.WriteLine(typePrompt ?? "Select backup type:");
                System.Console.WriteLine();

                WriteBackupTypeLine(0, selectedIndex, "1", full ?? "Full");
                WriteBackupTypeLine(1, selectedIndex, "2", differential ?? "Differential");

                string? hint = LangHelper.GetString("TuiNavigationHint");
                System.Console.WriteLine();
                System.Console.WriteLine(hint ?? "Use arrows to move, Enter/Space to validate, Esc to cancel.");

                ConsoleKeyInfo keyInfo = System.Console.ReadKey(intercept: true);
                switch (keyInfo.Key)
                {
                    case ConsoleKey.UpArrow:
                        selectedIndex = (selectedIndex + 2 - 1) % 2;
                        break;
                    case ConsoleKey.DownArrow:
                        selectedIndex = (selectedIndex + 1) % 2;
                        break;
                    case ConsoleKey.Enter:
                    case ConsoleKey.Spacebar:
                        return selectedIndex == 0 ? BackupType.Full : BackupType.Differential;
                    case ConsoleKey.Escape:
                        // Par défaut, on revient à une sauvegarde complète si l'utilisateur annule.
                        return BackupType.Full;
                    default:
                        // Support aussi la saisie directe "1"/"2".
                        char c = keyInfo.KeyChar;
                        if (c != '\0' && TryParseBackupType(c.ToString(), out BackupType parsed))
                            return parsed;
                        break;
                }
            }
        }

        private static void WriteBackupTypeLine(int index, int selectedIndex, string number, string text)
        {
            string prefix = index == selectedIndex ? ">" : " ";
            System.Console.WriteLine($"{prefix} {number}. {text}");
        }

        private static List<string> ParseCommaSeparatedList(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return new List<string>();
            return input!.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        private static async Task CreateJobAsync(IConfigurationRepository configRepository)
        {
            System.Console.WriteLine();
            string? createTitle = LangHelper.GetString("CreateJobTitle");
            System.Console.WriteLine(createTitle ?? "=== Create Backup Job ===");

            BackupConfiguration? config = await configRepository.LoadAsync(CancellationToken.None);
            if (config == null)
            {
                config = new BackupConfiguration
                {
                    Jobs = new List<BackupJob>(),
                    LastFullBackupUtcByJobId = new Dictionary<int, DateTime>()
                };
            }

            List<BackupJob> jobs = config.Jobs.ToList();

            if (jobs.Count >= 5)
            {
                string? maxJobsMsg = LangHelper.GetString("MaxJobsReached");
                System.Console.WriteLine(maxJobsMsg ?? "Maximum number of jobs (5) reached. Cannot create more jobs.");
                return;
            }

            int newJobId = jobs.Count > 0 ? jobs.Max(j => j.Id) + 1 : 1;
            if (newJobId > 5)
            {
                string? maxJobsMsg = LangHelper.GetString("MaxJobsReached");
                System.Console.WriteLine(maxJobsMsg ?? "Maximum number of jobs (5) reached. Cannot create more jobs.");
                return;
            }

            string? namePrompt = LangHelper.GetString("BackupName");
            System.Console.Write($"{namePrompt ?? "Enter backup name"}: ");
            string? name = System.Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                string? invalidInputMsg = LangHelper.GetString("InvalidInput");
                System.Console.WriteLine(invalidInputMsg ?? "Invalid input. Job creation cancelled.");
                return;
            }

            string? sourcePrompt = LangHelper.GetString("SourceDirectory");
            System.Console.Write($"{sourcePrompt ?? "Enter source directory"}: ");
            string? sourcePath = System.Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                string? invalidInputMsg = LangHelper.GetString("InvalidInput");
                System.Console.WriteLine(invalidInputMsg ?? "Invalid input. Job creation cancelled.");
                return;
            }

            string? targetPrompt = LangHelper.GetString("TargetDirectory");
            System.Console.Write($"{targetPrompt ?? "Enter target directory"}: ");
            string? targetPath = System.Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                string? invalidInputMsg = LangHelper.GetString("InvalidInput");
                System.Console.WriteLine(invalidInputMsg ?? "Invalid input. Job creation cancelled.");
                return;
            }

            BackupType backupType = ReadBackupType();

            string? extPrompt = LangHelper.GetString("ExcludeExtensionsPrompt");
            System.Console.WriteLine();
            System.Console.Write(extPrompt ?? "Exclude file extensions (comma-separated, example .tmp,.log). Leave empty for none: ");
            string? extInput = System.Console.ReadLine()?.Trim();
            List<string> excludeExtensions = ParseCommaSeparatedList(extInput);

            string? dirPrompt = LangHelper.GetString("ExcludeDirectoryNamesPrompt");
            System.Console.Write(dirPrompt ?? "Exclude file extensions (comma-separated, example .tmp,.log). Leave empty for none: ");
            string? dirInput = System.Console.ReadLine()?.Trim();
            List<string> excludeDirectoryNames = ParseCommaSeparatedList(dirInput);

            BackupJob newJob = new BackupJob
            {
                Id = newJobId,
                Name = name,
                SourcePath = sourcePath,
                TargetPath = targetPath,
                Type = backupType,
                ExcludeExtensions = excludeExtensions,
                ExcludeDirectoryNames = excludeDirectoryNames
            };

            jobs.Add(newJob);

            BackupConfiguration updatedConfig = new BackupConfiguration
            {
                LogAndStateDirectory = config.LogAndStateDirectory,
                Jobs = jobs,
                LastFullBackupUtcByJobId = config.LastFullBackupUtcByJobId
            };

            await configRepository.SaveAsync(updatedConfig, CancellationToken.None);

            string? jobCreatedMsg = LangHelper.GetString("JobCreated");
            if (!string.IsNullOrWhiteSpace(jobCreatedMsg))
            {
                System.Console.WriteLine(string.Format(jobCreatedMsg, newJobId));
            }
            else
            {
                System.Console.WriteLine($"Job {newJobId} created successfully.");
            }
        }

        private static async Task ListJobsAsync(IConfigurationRepository configRepository)
        {
            System.Console.WriteLine();
            string? listTitle = LangHelper.GetString("ListJobsTitle");
            System.Console.WriteLine(listTitle ?? "=== Backup Jobs ===");

            BackupConfiguration? config = await configRepository.LoadAsync(CancellationToken.None);
            if (config == null || config.Jobs.Count == 0)
            {
                string? noJobsMsg = LangHelper.GetString("NoJobsFound");
                System.Console.WriteLine(noJobsMsg ?? "No backup jobs found.");
                return;
            }

            foreach (BackupJob job in config.Jobs.OrderBy(j => j.Id))
            {
                string? fullBackup = LangHelper.GetString("FullBackup");
                string? differentialBackup = LangHelper.GetString("DifferentialBackup");
                string typeStr = job.Type == BackupType.Full
                    ? (fullBackup ?? "Full")
                    : (differentialBackup ?? "Differential");

                System.Console.WriteLine($"Job {job.Id}: {job.Name}");
                System.Console.WriteLine($"  Type: {typeStr}");
                System.Console.WriteLine($"  Source: {job.SourcePath}");
                System.Console.WriteLine($"  Target: {job.TargetPath}");
                System.Console.WriteLine();
            }
        }

        private static async Task RunJobsAsync(IConfigurationRepository configRepository, IBackupExecutor backupExecutor)
        {
            System.Console.WriteLine();
            string? runTitle = LangHelper.GetString("RunJobsTitle");
            System.Console.WriteLine(runTitle ?? "=== Run Backups ===");

            BackupConfiguration? config = await configRepository.LoadAsync(CancellationToken.None);
            if (config == null || config.Jobs.Count == 0)
            {
                string? noJobsMsg = LangHelper.GetString("NoJobsFound");
                System.Console.WriteLine(noJobsMsg ?? "No backup jobs found.");
                return;
            }

            await ListJobsAsync(configRepository);

            string? selectJobsPrompt = LangHelper.GetString("SelectJobsPrompt");
            System.Console.Write($"{selectJobsPrompt ?? "Enter job IDs to run (e.g., 1-3 or 1,3,5)"}: ");
            string? input = System.Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                string? invalidInputMsg = LangHelper.GetString("InvalidInput");
                System.Console.WriteLine(invalidInputMsg ?? "Invalid input. No jobs selected.");
                return;
            }

            List<int> jobIds = new List<int>();
            string[] parts = input.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Contains('-'))
                {
                    string[] rangeParts = trimmed.Split('-');
                    if (rangeParts.Length == 2 &&
                        int.TryParse(rangeParts[0].Trim(), out int start) &&
                        int.TryParse(rangeParts[1].Trim(), out int end))
                    {
                        for (int i = Math.Min(start, end); i <= Math.Max(start, end); i++)
                        {
                            if (i >= 1 && i <= 5 && !jobIds.Contains(i))
                                jobIds.Add(i);
                        }
                    }
                }
                else if (int.TryParse(trimmed, out int id) && id >= 1 && id <= 5 && !jobIds.Contains(id))
                {
                    jobIds.Add(id);
                }
            }

            if (jobIds.Count == 0)
            {
                string? invalidInputMsg = LangHelper.GetString("InvalidInput");
                System.Console.WriteLine(invalidInputMsg ?? "Invalid job IDs. No jobs selected.");
                return;
            }

            using CancellationTokenSource cts = new CancellationTokenSource();
            System.Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            string? backupStartMsg = LangHelper.GetString("BackupStart");
            System.Console.WriteLine(backupStartMsg ?? "Starting backup...");

            try
            {
                await backupExecutor.ExecuteAsync(jobIds, cts.Token);
                string? backupCompletedMsg = LangHelper.GetString("BackupCompleted");
                System.Console.WriteLine(backupCompletedMsg ?? "Backup completed successfully.");
            }
            catch (OperationCanceledException)
            {
                string? backupCancelMsg = LangHelper.GetString("BackupCancel");
                System.Console.WriteLine(backupCancelMsg ?? "Backup cancelled by user.");
            }
            catch (Exception ex)
            {
                string? backupErrorMsg = LangHelper.GetString("BackupError");
                System.Console.WriteLine($"{backupErrorMsg ?? "Backup failed"}: {ex.Message}");
            }
        }

        private static void ShowHelp()
        {
            System.Console.WriteLine();
            string? helpTitle = LangHelper.GetString("HelpTitle");
            System.Console.WriteLine(helpTitle ?? "=== Help ===");
            System.Console.WriteLine();

            string? helpText = LangHelper.GetString("HelpText");
            if (!string.IsNullOrWhiteSpace(helpText))
            {
                System.Console.WriteLine(helpText);
            }
            else
            {
                System.Console.WriteLine("EasySave - Backup Management System");
                System.Console.WriteLine();
                System.Console.WriteLine("Options:");
                System.Console.WriteLine("  1. Create a backup job - Add a new backup configuration");
                System.Console.WriteLine("  2. List backup jobs - Display all configured backup jobs");
                System.Console.WriteLine("  3. Run backups - Execute one or more backup jobs");
                System.Console.WriteLine("  4. Help - Show this help message");
                System.Console.WriteLine("  0. Quit - Exit the application");
                System.Console.WriteLine();
                System.Console.WriteLine("When running backups, you can specify job IDs as:");
                System.Console.WriteLine("  - Single ID: 1");
                System.Console.WriteLine("  - Range: 1-3");
                System.Console.WriteLine("  - List: 1,3,5 or 1;3;5");
            }
        }
    }
}

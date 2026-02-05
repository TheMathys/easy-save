using EasySave.Console.Resources;

namespace EasySave.Console.Tui
{
    /// <summary>Option Aide (option 7).</summary>
    public static partial class TuiRunner
    {
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
                System.Console.WriteLine("  4. Delete a backup job");
                System.Console.WriteLine("  5. Edit a backup job");
                System.Console.WriteLine("  6. View paths (config and logs)");
                System.Console.WriteLine("  7. Help - Show this help message");
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

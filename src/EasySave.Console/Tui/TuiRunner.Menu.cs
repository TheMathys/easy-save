using System;
using EasySave.Console.Resources;

namespace EasySave.Console.Tui
{
    /// <summary>Menu display and choice (arrows + numeric 1-7 / 0).</summary>
    public static partial class TuiRunner
    {
        /// <summary>
        /// Affiche le menu et récupère le choix (0=Create, 1=List, 2=Run, 3=Delete, 4=Edit, 5=ViewPaths, 6=Help, 7=Quit).
        /// </summary>
        private static int ShowMenuAndReadChoice()
        {
            if (!System.Console.IsInputRedirected)
                return ShowInteractiveMenu();

            while (true)
            {
                DisplayMenu();
                string? raw = System.Console.ReadLine()?.Trim().ToLowerInvariant();
                int mapped = MapTextChoiceToIndex(raw);
                if (mapped >= 0)
                    return mapped;
                string? errorMsg = LangHelper.GetString("MenuInvalidChoice");
                System.Console.WriteLine(errorMsg ?? "Invalid choice. Please select 1-7 or 0 to quit.");
            }
        }

        private static int ShowInteractiveMenu()
        {
            int selectedIndex = 0; // 0..7

            while (true)
            {
                System.Console.Clear();
                DisplayMenu(selectedIndex);

                string? hint = LangHelper.GetString("TuiNavigationHint");
                System.Console.WriteLine();
                System.Console.WriteLine(hint ?? "Arrows: move | Enter/Space: validate | Esc: quit | 1-7 / 0 / q: numeric selection");

                ConsoleKeyInfo keyInfo = System.Console.ReadKey(intercept: true);

                switch (keyInfo.Key)
                {
                    case ConsoleKey.UpArrow:
                        selectedIndex = (selectedIndex + 8 - 1) % 8;
                        break;
                    case ConsoleKey.DownArrow:
                        selectedIndex = (selectedIndex + 1) % 8;
                        break;
                    case ConsoleKey.Enter:
                    case ConsoleKey.Spacebar:
                        return selectedIndex;
                    case ConsoleKey.Escape:
                        return 7; // Quit
                    default:
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

        /// <summary>Maps "1".."7", "0", "q", "quit" to menu index 0..7 (-1 if invalid).</summary>
        private static int MapTextChoiceToIndex(string? choice)
        {
            if (string.IsNullOrWhiteSpace(choice)) return -1;
            string normalized = choice.Trim().ToLowerInvariant();
            return normalized switch
            {
                "1" => 0,
                "2" => 1,
                "3" => 2,
                "4" => 3,
                "5" => 4,
                "6" => 5,
                "7" => 6,
                "0" => 7,
                "q" => 7,
                "quit" => 7,
                _ => -1
            };
        }

        private static void DisplayMenu()
        {
            string? title = LangHelper.GetString("MenuTitle");
            System.Console.WriteLine(title ?? "=== EasySave Menu ===");
            System.Console.WriteLine();
            System.Console.WriteLine("1. " + (LangHelper.GetString("MenuOption1") ?? "Create a backup job"));
            System.Console.WriteLine("2. " + (LangHelper.GetString("MenuOption2") ?? "List backup jobs"));
            System.Console.WriteLine("3. " + (LangHelper.GetString("MenuOption3") ?? "Run backups"));
            System.Console.WriteLine("4. " + (LangHelper.GetString("TuiOptionDelete") ?? "Delete a backup job"));
            System.Console.WriteLine("5. " + (LangHelper.GetString("TuiOptionEdit") ?? "Edit a backup job"));
            System.Console.WriteLine("6. " + (LangHelper.GetString("TuiOptionViewPaths") ?? "View paths (config and logs)"));
            System.Console.WriteLine("7. " + (LangHelper.GetString("MenuOption4") ?? "Help"));
            System.Console.WriteLine("0. " + (LangHelper.GetString("MenuOption0") ?? "Quit"));
            System.Console.WriteLine();
            System.Console.Write(LangHelper.GetString("MenuPrompt") ?? "Enter your choice: ");
        }

        private static void DisplayMenu(int selectedIndex)
        {
            string? title = LangHelper.GetString("MenuTitle");
            System.Console.WriteLine(title ?? "=== EasySave Menu ===");
            System.Console.WriteLine();

            WriteMenuLine(0, selectedIndex, "1", LangHelper.GetString("MenuOption1") ?? "Create a backup job");
            WriteMenuLine(1, selectedIndex, "2", LangHelper.GetString("MenuOption2") ?? "List backup jobs");
            WriteMenuLine(2, selectedIndex, "3", LangHelper.GetString("MenuOption3") ?? "Run backups");
            WriteMenuLine(3, selectedIndex, "4", LangHelper.GetString("TuiOptionDelete") ?? "Delete a backup job");
            WriteMenuLine(4, selectedIndex, "5", LangHelper.GetString("TuiOptionEdit") ?? "Edit a backup job");
            WriteMenuLine(5, selectedIndex, "6", LangHelper.GetString("TuiOptionViewPaths") ?? "View paths (config and logs)");
            WriteMenuLine(6, selectedIndex, "7", LangHelper.GetString("MenuOption4") ?? "Help");
            WriteMenuLine(7, selectedIndex, "0", LangHelper.GetString("MenuOption0") ?? "Quit");
        }

        private static void WriteMenuLine(int index, int selectedIndex, string number, string? text)
        {
            string prefix = index == selectedIndex ? ">" : " ";
            System.Console.WriteLine($"{prefix} {number}. {text ?? ""}");
        }
    }
}

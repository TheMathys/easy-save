using System.Collections.ObjectModel;
using Avalonia.Controls;
using EasySave.Core.Entities;

namespace EasySave.Gui;

/// <summary>
/// Main window view: contains only generated XAML content,
/// the DataContext is provided by <see cref="App"/> (pure MVVM).
/// </summary>
public partial class MainWindow : Window
{
    public ObservableCollection<BackupJob> SelectedJobs { get; } = new();  // Auto-sync with CheckBox
    public string TabHeader => "Jobs";  // Ou resource localisé [web:67]

    
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}

using Avalonia.Controls;

namespace EasySave.Gui;

/// <summary>
/// Main window view: contains only generated XAML content,
/// the DataContext is provided by <see cref="App"/> (pure MVVM).
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}

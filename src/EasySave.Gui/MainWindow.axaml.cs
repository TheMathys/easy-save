using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace EasySave.Gui;

/// <summary>
/// Main window view: contains only generated XAML content,
/// the DataContext is provided by <see cref="App"/> (pure MVVM).
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var tabControl = this.FindControl<TabControl>("MainTabs");
        if (tabControl is null)
            return;

        tabControl.TemplateApplied += (_, e) =>
        {
            // Avalonia forces KeyboardNavigationMode.Once on the tab-strip panel,
            // which only tab-stops on the selected header and breaks Shift+Tab.
            // Switch to Continue so every TabItem is a normal tab stop.
            var itemsPresenter = e.NameScope.Find<ItemsPresenter>("PART_ItemsPresenter");
            if (itemsPresenter?.Panel is { } panel)
                KeyboardNavigation.SetTabNavigation(panel, KeyboardNavigationMode.Continue);
        };

        // By default TabControl only selects a tab via arrow-key (Directional) focus.
        // Allow Enter / Space to select the focused tab header as well.
        tabControl.AddHandler(KeyDownEvent, OnTabItemActivated, RoutingStrategies.Tunnel);
    }

    private void OnTabItemActivated(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space))
            return;

        var tabControl = this.FindControl<TabControl>("MainTabs");
        if (tabControl is null)
            return;

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is not TabItem focusedTab)
            return;

        var index = tabControl.IndexFromContainer(focusedTab);
        if (index >= 0)
        {
            tabControl.SelectedIndex = index;
            e.Handled = true;
        }
    }
}

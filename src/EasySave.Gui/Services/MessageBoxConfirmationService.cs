using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace EasySave.Gui.Services;

public sealed class MessageBoxConfirmationService : IConfirmationService
{
    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(12)
        };

        var text = new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap };

        var buttonsPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 12, 0, 0)
        };

        var cancelBtn = new Button { Content = "Cancel", IsCancel = true, Width = 90 };
        var deleteBtn = new Button { Content = "Delete", IsDefault = true, Width = 90 };

        buttonsPanel.Children.Add(cancelBtn);
        buttonsPanel.Children.Add(deleteBtn);

        panel.Children.Add(text);
        panel.Children.Add(buttonsPanel);

        var dlg = new Window
        {
            Title = title,
            Width = 400,
            Height = 160,
            CanResize = false,
            Content = panel
        };

        // Wire button clicks to close dialog with result
        cancelBtn.Click += (_, _) => dlg.Close(false);
        deleteBtn.Click += (_, _) => dlg.Close(true);

        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (lifetime?.MainWindow != null)
        {
            var result = await dlg.ShowDialog<bool?>(lifetime.MainWindow).ConfigureAwait(true);
            return result == true;
        }

        return false;
    }
}

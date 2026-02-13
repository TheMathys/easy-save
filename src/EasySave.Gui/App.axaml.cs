using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EasySave.Gui.Services;
using EasySave.Gui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace EasySave.Gui;

/// <summary>
/// Avalonia application class for the EasySave GUI.
/// Responsible for loading XAML resources and wiring the main window.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Global service provider used by the GUI to resolve services and ViewModels.
    /// Set once in <see cref="Program.Main"/>.
    /// </summary>
    public static IServiceProvider? ServiceProvider { get; set; }

    /// <inheritdoc />
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && ServiceProvider != null)
        {
            MainWindowViewModel mainVm = ServiceProvider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = mainVm };

            IFolderPickerService folderPicker = ServiceProvider.GetRequiredService<IFolderPickerService>();
            folderPicker.SetOwner(desktop.MainWindow);
            IFilePickerService filePicker = ServiceProvider.GetRequiredService<IFilePickerService>();
            filePicker.SetOwner(desktop.MainWindow);

            IConfigurationHolder configHolder = ServiceProvider.GetRequiredService<IConfigurationHolder>();
            // Trigger initial configuration load without blocking the UI thread.
            _ = configHolder.ReloadAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}

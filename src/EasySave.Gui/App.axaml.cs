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
            // When configuration changes (including initial load), update the application theme resources
            // (light/dark palette + text scale).
            configHolder.ConfigurationChanged += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var cfg = configHolder.Current;
                ApplyTheme(cfg.UseDarkTheme);
                ApplyTextScale(cfg.TextScalePercent);
            });
            // Trigger initial configuration load without blocking the UI thread. The ConfigurationChanged handler
            // will be invoked when ReloadAsync completes and will apply the theme and text scale.
            _ = configHolder.ReloadAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ApplyTheme(bool useDark)
    {
        // Update application-level resource colors to switch between light and dark palettes.
        var resources = this.Resources;
        if (resources == null) return;

        if (useDark)
        {
            resources["WindowBackgroundColor"] = Avalonia.Media.Color.Parse("#0B1220");
            resources["HeaderBackgroundColor"] = Avalonia.Media.Color.Parse("#071122");
            resources["HeaderBorderBrushColor"] = Avalonia.Media.Color.Parse("#1F2937");
            resources["CardBackgroundColor"] = Avalonia.Media.Color.Parse("#0E1522");
            resources["CardBorderBrushColor"] = Avalonia.Media.Color.Parse("#1F2937");
            resources["PrimaryColor"] = Avalonia.Media.Color.Parse("#1E90FF");
            resources["PrimaryHoverColor"] = Avalonia.Media.Color.Parse("#187bda");
            resources["PrimaryPressedColor"] = Avalonia.Media.Color.Parse("#1166b3");
            resources["PrimaryForegroundColor"] = Avalonia.Media.Color.Parse("#FFFFFFFF");
            resources["ForegroundPrimaryColor"] = Avalonia.Media.Color.Parse("#E6EEF8");
            resources["ForegroundSecondaryColor"] = Avalonia.Media.Color.Parse("#C7D2E6");
            resources["ForegroundTertiaryColor"] = Avalonia.Media.Color.Parse("#9AA7B8");
            resources["MutedColor"] = Avalonia.Media.Color.Parse("#E9EEF7");
            resources["ProgressBackgroundColor"] = Avalonia.Media.Color.Parse("#1F2937");
        }
        else
        {
            // Light theme (defaults)
            resources["WindowBackgroundColor"] = Avalonia.Media.Color.Parse("#FAFAFA");
            resources["HeaderBackgroundColor"] = Avalonia.Media.Color.Parse("#FFFFFF");
            resources["HeaderBorderBrushColor"] = Avalonia.Media.Color.Parse("#E5E7EB");
            resources["CardBackgroundColor"] = Avalonia.Media.Color.Parse("#FFFFFF");
            resources["CardBorderBrushColor"] = Avalonia.Media.Color.Parse("#E5E7EB");
            resources["PrimaryColor"] = Avalonia.Media.Color.Parse("#0067C0");
            resources["PrimaryHoverColor"] = Avalonia.Media.Color.Parse("#005A9E");
            resources["PrimaryPressedColor"] = Avalonia.Media.Color.Parse("#004578");
            resources["PrimaryForegroundColor"] = Avalonia.Media.Color.Parse("#FFFFFFFF");
            resources["ForegroundPrimaryColor"] = Avalonia.Media.Color.Parse("#111827");
            resources["ForegroundSecondaryColor"] = Avalonia.Media.Color.Parse("#374151");
            resources["ForegroundTertiaryColor"] = Avalonia.Media.Color.Parse("#4B5563");
            resources["MutedColor"] = Avalonia.Media.Color.Parse("#9CA3AF");
            resources["ProgressBackgroundColor"] = Avalonia.Media.Color.Parse("#E5E7EB");
        }
    }

    /// <summary>
    /// Applies the configured text scale to global font size resources so that
    /// text and controls become larger or smaller while keeping the layout intact.
    /// </summary>
    /// <param name="textScalePercent">Scale in percent, where 100 means default size.</param>
    private void ApplyTextScale(int textScalePercent)
    {
        var resources = this.Resources;
        if (resources == null)
            return;

        // Fallback to 100% if value is invalid or missing, and clamp to a safe range.
        if (textScalePercent <= 0)
            textScalePercent = 100;

        double scale = Math.Clamp(textScalePercent, 50, 200) / 100.0;

        // Base font sizes must stay in sync with App.axaml resources.
        const double baseBody = 13;
        const double baseSmall = 12;
        const double baseSectionTitle = 14;
        const double baseTabHeader = 14;
        const double baseHeaderTitle = 22;
        const double baseLogo = 18;
        const double baseBadge = 11;

        resources["FontSize_Body"] = baseBody * scale;
        resources["FontSize_Small"] = baseSmall * scale;
        resources["FontSize_SectionTitle"] = baseSectionTitle * scale;
        resources["FontSize_TabHeader"] = baseTabHeader * scale;
        resources["FontSize_HeaderTitle"] = baseHeaderTitle * scale;
        resources["FontSize_Logo"] = baseLogo * scale;
        resources["FontSize_Badge"] = baseBadge * scale;
    }
}

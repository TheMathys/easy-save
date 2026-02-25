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
            // (light/dark variant + text scale).
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
        RequestedThemeVariant = useDark
            ? Avalonia.Styling.ThemeVariant.Dark
            : Avalonia.Styling.ThemeVariant.Light;

        var resources = this.Resources;
        if (resources == null)
            return;

        if (useDark)
        {
            resources["WindowBackgroundColor"] = Avalonia.Media.Color.Parse("#020617");
            resources["HeaderBackgroundColor"] = Avalonia.Media.Color.Parse("#020617");
            resources["HeaderBorderBrushColor"] = Avalonia.Media.Color.Parse("#1E293B");
            resources["CardBackgroundColor"] = Avalonia.Media.Color.Parse("#020617");
            resources["CardBorderBrushColor"] = Avalonia.Media.Color.Parse("#1E293B");
            resources["PrimaryColor"] = Avalonia.Media.Color.Parse("#3B82F6");
            resources["PrimaryHoverColor"] = Avalonia.Media.Color.Parse("#2563EB");
            resources["PrimaryPressedColor"] = Avalonia.Media.Color.Parse("#1D4ED8");
            resources["PrimaryForegroundColor"] = Avalonia.Media.Color.Parse("#FFFFFFFF");
            resources["ForegroundPrimaryColor"] = Avalonia.Media.Color.Parse("#E5E7EB");
            resources["ForegroundSecondaryColor"] = Avalonia.Media.Color.Parse("#CBD5F5");
            resources["ForegroundTertiaryColor"] = Avalonia.Media.Color.Parse("#9CA3AF");
            resources["MutedColor"] = Avalonia.Media.Color.Parse("#6B7280");
            resources["ProgressBackgroundColor"] = Avalonia.Media.Color.Parse("#1E293B");
        }
        else
        {
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

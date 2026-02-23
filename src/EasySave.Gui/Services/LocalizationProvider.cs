using System.Collections;
using System.Globalization;
using System.Resources;
using System.Reflection;

namespace EasySave.Gui.Services;

/// <summary>
/// Default implementation of <see cref="ILocalizationProvider"/> using
/// strongly-typed access to ResX resources embedded in the GUI assembly.
/// </summary>
public sealed class LocalizationProvider : ILocalizationProvider
{
    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizationProvider"/> class.
    /// </summary>
    public LocalizationProvider()
    {
        var systemCulture = CultureInfo.InstalledUICulture;

        CultureInfo.CurrentCulture = systemCulture;
        CultureInfo.CurrentUICulture = systemCulture;
        CultureInfo.DefaultThreadCurrentCulture = systemCulture;
        CultureInfo.DefaultThreadCurrentUICulture = systemCulture;

        _resourceManager = new ResourceManager("EasySave.Gui.Resources.Strings", Assembly.GetExecutingAssembly());
        _currentCulture = systemCulture;
    }

    public string GetString(string key)
    {
        string? value = _resourceManager.GetString(key, _currentCulture);
        return value ?? key;
    }

    public string GetString(string key, params object[] args)
    {
        string? format = GetString(key);
        return args.Length > 0 ? string.Format(format, args) : format;
    }

    public event EventHandler? CultureChanged;

    public void SetCulture(string cultureCode)
    {
        _currentCulture = CultureInfo.GetCultureInfo(cultureCode);
        CultureInfo.CurrentCulture = _currentCulture;
        CultureInfo.CurrentUICulture = _currentCulture;
        CultureInfo.DefaultThreadCurrentCulture = _currentCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _currentCulture;
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    public Array GetLanguages()
    {
        var languages = new System.Collections.Generic.List<string>();

        var resourceSet = _resourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
        if (resourceSet == null) return languages.ToArray();

        const string prefix = "Gui_LabelLang";

        foreach (DictionaryEntry entry in resourceSet)
        {
            if (entry.Key is string key && key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                // Get the resource value (display name like "English", "French")
                string? displayName = entry.Value?.ToString();
                if (!string.IsNullOrWhiteSpace(displayName) && !languages.Contains(displayName))
                {
                    languages.Add(displayName);
                }
            }
        }

        return languages.ToArray();
    }
}

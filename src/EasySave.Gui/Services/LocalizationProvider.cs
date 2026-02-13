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
        _resourceManager = new ResourceManager("EasySave.Gui.Resources.Strings", Assembly.GetExecutingAssembly());
        _currentCulture = CultureInfo.CurrentUICulture;
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
}

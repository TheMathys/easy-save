namespace EasySave.Gui.Services;

/// <summary>
/// Provides localized UI strings for the GUI (inversion of control abstraction).
/// </summary>
public interface ILocalizationProvider
{
    /// <summary>
    /// Gets a localized string for the given key using the current culture.
    /// </summary>
    string GetString(string key);

    /// <summary>
    /// Gets a formatted localized string for the given key.
    /// </summary>
    string GetString(string key, params object[] args);

    /// <summary>
    /// Raised when the culture changes so that bindings can refresh.
    /// </summary>
    event EventHandler? CultureChanged;

    /// <summary>
    /// Sets the UI culture (e.g. <c>"fr"</c>, <c>"en"</c>).
    /// </summary>
    /// <param name="cultureCode">Culture code to apply.</param>
    void SetCulture(string cultureCode);

    /// <summary>
    /// Gets the list of available languages supported by the provider.
    /// </summary>
    /// <returns>List of available languages</returns>
    Array GetLanguages();
}

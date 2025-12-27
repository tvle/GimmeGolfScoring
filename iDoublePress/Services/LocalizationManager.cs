using System.Globalization;

namespace iDoublePress.Services;

/// <summary>
/// Manages application localization and culture changes
/// </summary>
public class LocalizationManager
{
    private static readonly Lazy<LocalizationManager> _instance = new(() => new LocalizationManager());
    public static LocalizationManager Instance => _instance.Value;

    public event EventHandler<CultureChangedEventArgs>? CultureChanged;

    private LocalizationManager()
    {
    }

    /// <summary>
    /// Sets the application culture to the specified culture
    /// </summary>
    public void SetCulture(CultureInfo culture)
    {
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        CultureChanged?.Invoke(this, new CultureChangedEventArgs(culture));
    }

    /// <summary>
    /// Gets the current UI culture
    /// </summary>
    public CultureInfo GetCurrentCulture()
    {
        return CultureInfo.CurrentUICulture;
    }

    /// <summary>
    /// Gets list of cultures supported by the application
    /// </summary>
    public List<CultureInfo> GetSupportedCultures()
    {
        return new List<CultureInfo>
        {
            new CultureInfo("en"),    // English
            new CultureInfo("es"),    // Spanish (Español)
            new CultureInfo("fr"),    // French (Français)
            new CultureInfo("de"),    // German (Deutsch)
            new CultureInfo("ja"),    // Japanese (???)
            new CultureInfo("ko"),    // Korean (???)
            new CultureInfo("zh-CN"), // Simplified Chinese (????)
            new CultureInfo("it"),    // Italian (Italiano)
            new CultureInfo("pt"),    // Portuguese (Português)
            new CultureInfo("sv"),    // Swedish (Svenska)
        };
    }

    /// <summary>
    /// Gets display name for a culture (in native language)
    /// </summary>
    public string GetCultureDisplayName(CultureInfo culture)
    {
        return culture.NativeName;
    }

    /// <summary>
    /// Loads saved language preference from app preferences
    /// </summary>
    public void LoadSavedLanguage()
    {
        var savedLanguage = Preferences.Default.Get("app_language", string.Empty);
        
        if (!string.IsNullOrEmpty(savedLanguage))
        {
            try
            {
                var culture = new CultureInfo(savedLanguage);
                SetCulture(culture);
            }
            catch
            {
                // If saved language is invalid, use default (English)
                SetCulture(new CultureInfo("en"));
            }
        }
    }

    /// <summary>
    /// Saves language preference to app preferences
    /// </summary>
    public void SaveLanguagePreference(CultureInfo culture)
    {
        Preferences.Default.Set("app_language", culture.Name);
    }
}

/// <summary>
/// Event args for culture change notifications
/// </summary>
public class CultureChangedEventArgs : EventArgs
{
    public CultureInfo NewCulture { get; }

    public CultureChangedEventArgs(CultureInfo newCulture)
    {
        NewCulture = newCulture;
    }
}

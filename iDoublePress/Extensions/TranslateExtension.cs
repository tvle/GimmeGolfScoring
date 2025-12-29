using System.Globalization;

namespace iDoublePress.Extensions;

/// <summary>
/// Markup extension for localized strings in XAML
/// Usage: Text="{ext:Translate KeyName}"
/// </summary>
[ContentProperty(nameof(Key))]
public class TranslateExtension : IMarkupExtension
{
    public string? Key { get; set; }

    public object? ProvideValue(IServiceProvider serviceProvider)
    {
        if (Key == null)
            return null;

        var resourceManager = Resources.Strings.AppResources.ResourceManager;
        return resourceManager.GetString(Key, CultureInfo.CurrentUICulture) ?? Key;
    }
}

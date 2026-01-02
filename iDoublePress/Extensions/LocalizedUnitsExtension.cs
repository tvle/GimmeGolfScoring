using System.Globalization;

namespace iDoublePress.Extensions;

[ContentProperty(nameof(Key))]
public sealed class LocalizedUnitsExtension : IMarkupExtension
{
    public string? Key { get; set; }

    public object ProvideValue(IServiceProvider serviceProvider)
    {
        var key = Key;
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        var region = RegionInfo.CurrentRegion.TwoLetterISORegionName;
        var isUs = string.Equals(region, "US", StringComparison.OrdinalIgnoreCase);

        return key switch
        {
            "ProximityS" => isUs ? "< 6 ft" : "< 2 m",
            "ProximityM" => isUs ? "6–20 ft" : "2–6 m",
            "ProximityL" => isUs ? "> 20 ft" : "> 6 m",
            _ => key,
        };
    }
}

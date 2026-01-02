using System.Globalization;

namespace iDoublePress.Converters;

public class NullableIntToDashConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            null => "--",
            int i => i.ToString(culture),
            _ => "--"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            var trimmed = s.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed is "-" or "--")
                return null;

            if (int.TryParse(trimmed, NumberStyles.Integer, culture, out var i))
                return i;
        }

        return null;
    }
}

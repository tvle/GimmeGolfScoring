using System.Globalization;
using iDoublePress.Models;

namespace iDoublePress.Converters;

public class FairwayPenaltyEnabledConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not FairwayResult result)
            return false;

        return result is FairwayResult.Left or FairwayResult.Right;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

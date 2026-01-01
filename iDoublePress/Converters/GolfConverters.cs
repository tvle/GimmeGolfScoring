using System.Globalization;

namespace iDoublePress.Converters;

public class SubtractOneConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Syncfusion segmented controls use -1 to represent "no selection".
        // Our model uses 0 (FairwayResult.None) for the same meaning.
        if (value is int intValue)
        {
            return intValue == 0 ? -1 : intValue - 1;
        }

        return -1;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Convert SelectedIndex back to FairwayResult int value.
        // -1 => None(0), 0 => Left(1), 1 => Fairway(2), 2 => Right(3)
        if (value is int index)
        {
            return index < 0 ? 0 : index + 1;
        }

        return 0;
    }
}

public class BoolToCompleteTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isLastHole && isLastHole)
        {
            return "Complete Round";
        }
        return "Continue";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class IsNotZeroConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue != 0;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

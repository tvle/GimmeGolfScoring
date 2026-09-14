using System.Globalization;

namespace iDoublePress.Data;

public static class DatabaseDateTime
{
    public static DateTime EnsureUtc(DateTime value)
    {
        if (value == default)
            return DateTime.UtcNow;

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime()
        };
    }

    public static string ToUtcString(DateTime value) => EnsureUtc(value).ToString("o", CultureInfo.InvariantCulture);

    public static DateTime ParseUtc(string value)
    {
        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return EnsureUtc(parsed);
        }

        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out parsed))
        {
            return EnsureUtc(parsed);
        }

        return EnsureUtc(DateTime.Parse(value, CultureInfo.InvariantCulture));
    }
}

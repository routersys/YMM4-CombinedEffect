using System.Globalization;
using System.Windows.Data;

namespace CombinedEffect.Converters;

public sealed class UtcToLocalTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime time) return value;
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(time, TimeZoneInfo.Local).ToString("g", culture);
        }
        catch
        {
            return time.ToLocalTime().ToString("g", culture);
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
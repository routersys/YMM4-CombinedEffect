using System.Globalization;
using System.Windows.Data;

namespace CombinedEffect.Converters;

internal sealed class WidthThresholdConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double width)
            return false;

        if (parameter is null)
            return false;

        if (!double.TryParse(parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold))
            return false;

        return width < threshold;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
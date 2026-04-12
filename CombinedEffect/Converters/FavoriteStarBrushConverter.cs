using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace CombinedEffect.Converters;

internal sealed class FavoriteStarBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00))
            : SystemColors.GrayTextBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

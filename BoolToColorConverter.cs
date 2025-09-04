using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CombinedEffect.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isFavorite && isFavorite)
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700")); // Gold
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
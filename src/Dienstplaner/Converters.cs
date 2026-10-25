using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Dienstplaner;

public static class Converters
{
    public static readonly IValueConverter BoolToVisibility = new BooleanToVisibilityConverter();
}

public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b) return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LicorpExportPlus.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public static readonly BooleanToVisibilityConverter Instance = new BooleanToVisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
return boolValue ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
			}
			return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is System.Windows.Visibility visibility && visibility == System.Windows.Visibility.Visible;
        }
    }

    public class StringIsEmptyConverter : IValueConverter
    {
        public static readonly StringIsEmptyConverter Instance = new StringIsEmptyConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}

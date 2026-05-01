using System;
using System.Globalization;
using System.Windows.Data;
using LicorpExportPlus.Models;

namespace LicorpExportPlus.Views
{
    /// <summary>
    /// Converter để đảo ngược Boolean values cho RadioButton binding
    /// </summary>
    public class InverseBooleanConverter : IValueConverter
    {
        public static readonly InverseBooleanConverter Instance = new InverseBooleanConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;

            return false;
        }
    }

    /// <summary>
    /// Converter cho Enum to Boolean binding trong RadioButtons
    /// </summary>
    public class EnumToBooleanConverter : IValueConverter
    {
        public static readonly EnumToBooleanConverter Instance = new EnumToBooleanConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            return value.Equals(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue && parameter != null)
                return parameter;

            return System.Windows.Data.Binding.DoNothing;
        }
    }

    /// <summary>
    /// Converter để format số lượng sheets đã chọn
    /// </summary>
    public class SelectedCountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return $"Đã chọn {count} sheets";
            }

            return "Chưa chọn sheets nào";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// Converter để hiển thị format list
    /// </summary>
    public class FormatListConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Collections.Generic.Dictionary<string, bool> formats)
            {
                var selectedFormats = new System.Collections.Generic.List<string>();
                foreach (var format in formats)
                {
                    if (format.Value)
                        selectedFormats.Add(format.Key);
                }

                return selectedFormats.Count > 0 ? string.Join(", ", selectedFormats) : "Chưa chọn format nào";
            }

            return "Không có format";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}

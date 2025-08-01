using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace VideoConversion_ClientTo.Presentation.Converters
{
    /// <summary>
    /// 布尔值到颜色的转换器
    /// </summary>
    public class BooleanToColorConverter : IValueConverter
    {
        public static readonly BooleanToColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Colors.Green : Colors.Red;
            }
            return Colors.Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

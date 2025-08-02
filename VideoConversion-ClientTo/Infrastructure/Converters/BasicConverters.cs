using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace VideoConversion_ClientTo.Infrastructure.Converters
{
    /// <summary>
    /// 布尔值到字符串转换器
    /// </summary>
    public class BoolToStringConverter : IValueConverter
    {
        public static readonly BoolToStringConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string paramString)
            {
                var parts = paramString.Split('|');
                if (parts.Length == 2)
                {
                    return boolValue ? parts[0] : parts[1];
                }
            }
            return value?.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 字符串到布尔值转换器
    /// </summary>
    public class StringToBoolConverter : IValueConverter
    {
        public static readonly StringToBoolConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                return !string.IsNullOrWhiteSpace(stringValue) && stringValue != "未测试";
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 整数到字符串转换器
    /// </summary>
    public class IntToStringConverter : IValueConverter
    {
        public static readonly IntToStringConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue && int.TryParse(stringValue, out var intValue))
            {
                return intValue;
            }
            return 0;
        }
    }
}

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DrillIntel.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        var options = parameter as string ?? string.Empty;

        if (options.Contains("Invert", StringComparison.OrdinalIgnoreCase))
            flag = !flag;

        if (flag) return Visibility.Visible;

        return options.Contains("Hidden", StringComparison.OrdinalIgnoreCase)
            ? Visibility.Hidden
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isVisible = value is Visibility v && v == Visibility.Visible;
        var options = parameter as string ?? string.Empty;
        if (options.Contains("Invert", StringComparison.OrdinalIgnoreCase))
            isVisible = !isVisible;
        return isVisible;
    }
}

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => !(value is bool b && b);

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
        => !(value is bool b && b);
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        return flag ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isVisible = value is Visibility v && v == Visibility.Visible;
        return !isVisible;
    }
}


public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isNotNull = value != null;
        if (parameter is string param && param.Contains("Invert", StringComparison.OrdinalIgnoreCase))
            isNotNull = !isNotNull;
        return isNotNull ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return Visibility.Collapsed;
        return value.ToString() == parameter.ToString() ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return value.ToString() == parameter.ToString();
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter != null)
            return Enum.Parse(targetType, parameter.ToString()!);
        return Binding.DoNothing;
    }
}

public class IntToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return value.ToString() == parameter.ToString();
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter != null)
        {
            if (int.TryParse(parameter.ToString(), out int intVal))
                return intVal;
        }
        return Binding.DoNothing;
    }
}

public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class HexToBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.BrushConverter _brushConverter = new();

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                string h = hex.Trim();
                if (!h.StartsWith("#", StringComparison.Ordinal) && (h.Length == 6 || h.Length == 8))
                {
                    h = "#" + h;
                }
                var brush = (System.Windows.Media.Brush?)_brushConverter.ConvertFromString(h);
                if (brush != null)
                {
                    if (brush.CanFreeze) brush.Freeze();
                    return brush;
                }
            }
            catch { }
        }
        return System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}


using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AFMS.App.Converters;

public sealed class BooleanNegationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is false;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is false;
}

/// <summary>Shows an element only when the bound flag is false.</summary>
public sealed class BooleanToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Shows an element only when the bound value is present and not blank.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            null => Visibility.Collapsed,
            string text when string.IsNullOrWhiteSpace(text) => Visibility.Collapsed,
            _ => Visibility.Visible
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>Highlights the navigation button matching the current section.</summary>
public sealed class StringEqualityMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length >= 2 &&
        string.Equals(values[0]?.ToString(), values[1]?.ToString(), StringComparison.Ordinal);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        targetTypes.Select(_ => Binding.DoNothing).ToArray();
}

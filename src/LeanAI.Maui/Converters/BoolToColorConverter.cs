using System.Globalization;

namespace LeanAI.Maui.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var parts = (parameter as string)?.Split('|');
        if (parts?.Length != 2) return Colors.Transparent;

        bool flag = value is bool b && b;
        var key = flag ? parts[0] : parts[1];

        return Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var resource) == true
            ? resource
            : Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

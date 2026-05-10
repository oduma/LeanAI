using System.Globalization;
using LeanAI.Domain.WeightManagement.Enums;

namespace LeanAI.Maui.Converters;

public class GenderToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var selected = value is Gender g ? g.ToString() : null;
        bool isActive = selected == (parameter as string);

        var key = isActive ? "ColorCopper" : "ColorNickel";
        return Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var resource) == true
            ? resource
            : Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

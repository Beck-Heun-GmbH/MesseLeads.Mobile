using System.Globalization;

namespace MesseLeads.Mobile.Converters;

public sealed class SelectionBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var selected = value is bool b && b;

        if (selected)
            return Color.FromArgb("#004D9D");

        return Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#26312D")
            : Color.FromArgb("#EEF1EF");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
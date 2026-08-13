using System.Globalization;

namespace MesseLeads.Mobile.Converters;

public sealed class CropOverlayBoundsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var left = Read(values, 0, 0.05);
        var top = Read(values, 1, 0.12);
        var right = Read(values, 2, 0.95);
        var bottom = Read(values, 3, 0.88);

        left = Math.Clamp(left, 0d, 0.95d);
        top = Math.Clamp(top, 0d, 0.95d);
        right = Math.Clamp(right, left + 0.05d, 1d);
        bottom = Math.Clamp(bottom, top + 0.05d, 1d);

        var cropWidth = right - left;
        var cropHeight = bottom - top;

        return (parameter as string) switch
        {
            "Top" => new Rect(0, 0, 1, top),
            "Bottom" => new Rect(0, bottom, 1, 1 - bottom),
            "Left" => new Rect(0, top, left, cropHeight),
            "Right" => new Rect(right, top, 1 - right, cropHeight),
            _ => new Rect(left, top, cropWidth, cropHeight)
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static double Read(IReadOnlyList<object> values, int index, double fallback)
    {
        if (index >= values.Count)
        {
            return fallback;
        }

        return values[index] switch
        {
            double value => value,
            float value => value,
            int value => value,
            _ => fallback
        };
    }
}

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Dynasland.App.Converters;

/// <summary>Converte um raio (double, animável) em <see cref="CornerRadius"/> uniforme.</summary>
public sealed class DoubleToCornerRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double radius ? new CornerRadius(radius) : new CornerRadius(0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is CornerRadius radius ? radius.TopLeft : 0d;
}

using System.Globalization;
using System.Windows.Data;

namespace Dynasland.App.Converters;

/// <summary>Fração (0 a 1) para um comprimento em pixels, até <see cref="Maximum"/> (barras de gráfico).</summary>
public sealed class FractionToLengthConverter : IValueConverter
{
    public double Maximum { get; set; } = 100;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double fraction ? Math.Clamp(fraction, 0, 1) * Maximum : 0.0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

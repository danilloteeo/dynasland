using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Dynasland.Core.Models;

namespace Dynasland.App.Converters;

/// <summary>
/// Raio da cápsula (double, animável) e formato da ilha em <see cref="CornerRadius"/>: flutuante arredonda
/// os quatro cantos; encaixada deixa os de cima retos, colados na borda da tela.
/// </summary>
public sealed class IslandCornerRadiusConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var radius = values.Length > 0 && values[0] is double r ? r : 0;
        var notch = values.Length > 1 && values[1] is IslandShape.Notch;
        return notch ? new CornerRadius(0, 0, radius, radius) : new CornerRadius(radius);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

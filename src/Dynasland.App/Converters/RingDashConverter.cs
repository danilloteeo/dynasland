using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Dynasland.App.Converters;

/// <summary>
/// Progresso (0 a 1) vira o tracejado de uma elipse, para desenhar um anel parcial em volta de um ícone.
/// A elipse de 38 px (o botão da dock) com traço de 2 px tem circunferência de π·36 ≈ 113 px, ou 56,5 unidades
/// de traço (o tracejado conta em múltiplos da espessura).
/// </summary>
public sealed class RingDashConverter : IValueConverter
{
    /// <summary>Circunferência do anel, em unidades de espessura do traço.</summary>
    public const double CircumferenceUnits = Math.PI * (38 - 2) / 2;

    public static DoubleCollection Dashes(double progress)
    {
        var filled = Math.Clamp(progress, 0, 1) * CircumferenceUnits;
        // O segundo valor é o vazio: maior que a volta inteira, para o traço não se repetir.
        return [filled, CircumferenceUnits * 2];
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Dashes(value is double progress ? progress : 0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

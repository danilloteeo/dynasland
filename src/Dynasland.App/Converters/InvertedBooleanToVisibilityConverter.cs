using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Dynasland.App.Converters;

/// <summary>Visível quando o valor é <c>false</c> (ex.: mensagem de lista vazia).</summary>
public sealed class InvertedBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

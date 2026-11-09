using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Dynasland.App.Converters;

/// <summary>Enum ↔ bool para RadioButtons: <c>true</c> quando o valor é igual ao parâmetro (nome do membro).</summary>
public sealed class EnumMatchConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true && parameter is string name ? Enum.Parse(targetType, name) : Binding.DoNothing;
}

/// <summary>Visível quando o valor do enum é igual ao parâmetro.</summary>
public sealed class EnumMatchToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString() ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

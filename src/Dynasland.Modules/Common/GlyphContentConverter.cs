using System.Globalization;
using System.Windows.Data;

namespace Dynasland.Modules.Common;

/// <summary>
/// Converte uma string de glifo em conteúdo: chaves "vector:..." conhecidas viram <see cref="VectorIcon"/>
/// (desenhado pelo DataTemplate implícito da aplicação); qualquer outro valor passa intacto.
/// Com o parâmetro <see cref="GlyphPart"/> devolve só a parte de fonte (vazio para ícone vetorial) e com
/// <see cref="VectorPart"/> só a vetorial (null para glifo de fonte), para templates que sobrepõem os dois.
/// </summary>
public sealed class GlyphContentConverter : IValueConverter
{
    public const string GlyphPart = "glyph";
    public const string VectorPart = "vector";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var icon = VectorIcons.Get(value as string);
        return parameter switch
        {
            GlyphPart => icon is null ? value : string.Empty,
            VectorPart => icon,
            _ => icon ?? value,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

namespace Dynasland.Modules.Common;

/// <summary>
/// Ícones vetoriais próprios. Onde o contrato é uma string de glifo (ex.: <c>IslandShortcut.Glyph</c>, Tag do menu),
/// usa-se a chave "vector:..." e o <see cref="GlyphContentConverter"/> troca a chave pelo <see cref="VectorIcon"/>.
/// </summary>
public static class VectorIcons
{
    public const string Prefix = "vector:";

    /// <summary>Gota d'água com reflexo (módulo Água).</summary>
    public const string Water = Prefix + "water";

    private static readonly Dictionary<string, VectorIcon> Icons = new(StringComparer.Ordinal)
    {
        [Water] = new VectorIcon(
            Outline: "M12,2.25 C12,2.25 5.25,9.75 5.25,15 A6.75,6.75 0 0 0 18.75,15 C18.75,9.75 12,2.25 12,2.25 Z",
            Detail: "M9,15 A3,3 0 0 0 12,18"),
    };

    /// <summary>O ícone da chave, ou null se a chave não for de um ícone vetorial conhecido.</summary>
    public static VectorIcon? Get(string? key) => key is not null && Icons.TryGetValue(key, out var icon) ? icon : null;
}

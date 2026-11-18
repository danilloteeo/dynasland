using System.Globalization;

namespace DynaIsland.Core.Services;

/// <summary>
/// Cor de destaque do visual Vidro: as cores prontas, o padrão e o clareamento que garante texto escuro
/// (#0D1220) legível em cima de qualquer destaque.
/// </summary>
public static class AccentColors
{
    public const string Default = "#9CC2FF";

    /// <summary>Padrão até a 1.8.x (branco); quem ainda o tem salvo passa para <see cref="Default"/>.</summary>
    public const string LegacyDefault = "#FFFFFF";

    /// <summary>Luminância relativa mínima para o texto escuro ficar legível sobre o destaque.</summary>
    public const double MinLuminance = 0.35;

    public static readonly IReadOnlyList<(string Name, string Hex)> Presets =
    [
        ("Azul-gelo", "#9CC2FF"),
        ("Menta", "#8FE3C8"),
        ("Limão", "#C6F432"),
        ("Âmbar", "#FFC27A"),
        ("Rosa", "#FF9EB5"),
        ("Lavanda", "#C7A6FF"),
    ];

    /// <summary>A cor em #RRGGBB, misturada com branco em passos de 5% até atingir <see cref="MinLuminance"/>.</summary>
    public static string EnsureReadable(byte r, byte g, byte b)
    {
        double red = r, green = g, blue = b;
        for (var step = 0; step < 20 && RelativeLuminance(Round(red), Round(green), Round(blue)) < MinLuminance; step++)
        {
            red += (255 - red) * 0.05;
            green += (255 - green) * 0.05;
            blue += (255 - blue) * 0.05;
        }

        return string.Create(CultureInfo.InvariantCulture, $"#{Round(red):X2}{Round(green):X2}{Round(blue):X2}");
    }

    /// <summary>Luminância relativa (WCAG) de 0 a 1.</summary>
    public static double RelativeLuminance(byte r, byte g, byte b)
    {
        static double Linear(byte channel)
        {
            var s = channel / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Linear(r) + 0.7152 * Linear(g) + 0.0722 * Linear(b);
    }

    private static byte Round(double value) => (byte)Math.Round(Math.Clamp(value, 0, 255));
}

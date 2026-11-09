using System.Globalization;

namespace Dynasland.Modules.Common;

public static class TextFormat
{
    /// <summary>Tempo de mídia: <c>3:07</c> ou <c>1:02:45</c>.</summary>
    public static string MediaTime(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : value.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    /// <summary>Cronômetro/temporizador: <c>05:00</c> ou <c>1:02:45</c>.</summary>
    public static string Timer(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes:00}:{value.Seconds:00}";
    }

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Tamanho em bytes: <c>850 KB</c>, <c>12,4 MB</c>, <c>1,5 GB</c>.</summary>
    public static string Bytes(double bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var unit = 0;
        while (bytes >= 1024 && unit < units.Length - 1)
        {
            bytes /= 1024;
            unit++;
        }

        var format = unit == 0 || bytes >= 100 ? "0" : "0.#";
        return $"{bytes.ToString(format, PtBr)} {units[unit]}";
    }

    /// <summary>Duração aproximada: <c>2 h 15 min</c>, <c>40 min</c>, <c>menos de 1 min</c>.</summary>
    public static string Duration(TimeSpan value)
    {
        var totalMinutes = (int)value.TotalMinutes;
        if (totalMinutes < 1)
        {
            return "menos de 1 min";
        }

        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        return hours == 0 ? $"{minutes} min" : minutes == 0 ? $"{hours} h" : $"{hours} h {minutes} min";
    }
}

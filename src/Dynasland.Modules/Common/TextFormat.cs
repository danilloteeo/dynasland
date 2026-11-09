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

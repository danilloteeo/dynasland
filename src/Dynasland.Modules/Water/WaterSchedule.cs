namespace Dynasland.Modules.Water;

/// <summary>Quando lembrar de beber água: horários fixos a partir do início do dia ativo.</summary>
public static class WaterSchedule
{
    /// <summary>
    /// Primeiro horário estritamente depois de <paramref name="localNow"/>, entre
    /// <paramref name="startHour"/> (inclusive) e <paramref name="endHour"/> (exclusive), a cada
    /// <paramref name="intervalMinutes"/>. Passado o último do dia, é o início do dia seguinte.
    /// </summary>
    public static DateTimeOffset Next(DateTimeOffset localNow, int startHour, int endHour, int intervalMinutes)
    {
        var step = TimeSpan.FromMinutes(intervalMinutes);
        var start = localNow.Date.AddHours(startHour);
        var end = localNow.Date.AddHours(endHour);
        for (var slot = start; slot < end; slot += step)
        {
            if (slot > localNow.DateTime)
            {
                return new DateTimeOffset(slot, localNow.Offset);
            }
        }

        return new DateTimeOffset(start.AddDays(1), localNow.Offset);
    }

    /// <summary>O horário cai no dia ativo (<paramref name="startHour"/> inclusive, <paramref name="endHour"/> exclusive).</summary>
    public static bool IsActive(DateTimeOffset localTime, int startHour, int endHour)
    {
        var time = localTime.DateTime;
        return time >= time.Date.AddHours(startHour) && time < time.Date.AddHours(endHour);
    }
}

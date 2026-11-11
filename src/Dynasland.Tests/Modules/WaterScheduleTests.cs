using Dynasland.Modules.Water;

namespace Dynasland.Tests.Modules;

public class WaterScheduleTests
{
    private static DateTimeOffset At(int day, int hour, int minute) => new(2026, 11, day, hour, minute, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(11, 8, 30, 11, 9, 0)]   // dentro do dia: próximo horário cheio
    [InlineData(11, 9, 0, 11, 10, 0)]   // exatamente num horário: o seguinte
    [InlineData(11, 7, 0, 11, 8, 0)]    // antes do início: o início
    [InlineData(11, 21, 30, 12, 8, 0)]  // 22h não vale (fim exclusivo): amanhã
    [InlineData(11, 23, 30, 12, 8, 0)]  // depois do fim: amanhã
    public void Next_reminder_follows_the_active_window(int d, int h, int m, int ed, int eh, int em) =>
        Assert.Equal(At(ed, eh, em), WaterSchedule.Next(At(d, h, m), startHour: 8, endHour: 22, intervalMinutes: 60));

    [Fact]
    public void Interval_longer_than_an_hour_stays_aligned_to_the_start()
    {
        // 8:00, 8:45, 9:30 ...
        Assert.Equal(At(11, 9, 30), WaterSchedule.Next(At(11, 9, 0), 8, 22, 45));
    }

    [Fact]
    public void Offset_of_the_local_clock_is_preserved()
    {
        var now = new DateTimeOffset(2026, 11, 11, 8, 30, 0, TimeSpan.FromHours(-3));
        Assert.Equal(new DateTimeOffset(2026, 11, 11, 9, 0, 0, TimeSpan.FromHours(-3)), WaterSchedule.Next(now, 8, 22, 60));
    }
}

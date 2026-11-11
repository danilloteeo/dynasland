using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Modules.Clock;
using Dynasland.Modules.Life;
using Dynasland.Modules.Media;
using Microsoft.Extensions.Time.Testing;

namespace Dynasland.Tests.Modules;

public class IdleAndTintTests : IDisposable
{
    private static readonly DateTimeOffset Monday = new(2026, 11, 9, 7, 5, 0, TimeSpan.Zero);

    private readonly ActivityRecorder _recorder = new();

    public void Dispose() => _recorder.Dispose();

    [Fact]
    public void Idle_shows_the_date_by_default()
    {
        var content = new ClockContent();
        content.Update(Monday);

        Assert.True(content.IsIdleVisible);
        Assert.False(content.IsIdleTimeOnly);
        Assert.False(content.HasIdleGlyph);
        Assert.Equal("seg, 9 nov", content.IdleText);
    }

    [Fact]
    public void Idle_shows_the_chosen_glance_in_its_short_form()
    {
        var content = new ClockContent();
        content.Update(Monday);
        IslandGlance[] glances =
        [
            new(WeatherModule.GlanceId, "W", "23° Nublado · São Paulo", ShortText: "23° Nublado"),
            new(CalendarModule.GlanceId, "C", "Dentista · hoje às 15:00"),
        ];

        content.SetIdle(IdleDisplay.Weather, glances);
        Assert.Equal("23° Nublado", content.IdleText);
        Assert.Equal("W", content.IdleGlyph);

        content.SetIdle(IdleDisplay.NextEvent, glances);
        Assert.Equal("Dentista · hoje às 15:00", content.IdleText);
        Assert.True(content.HasIdleGlyph);
    }

    [Fact]
    public void Idle_falls_back_to_the_date_when_the_glance_is_missing()
    {
        var content = new ClockContent();
        content.Update(Monday);

        content.SetIdle(IdleDisplay.Weather, []);

        Assert.Equal("seg, 9 nov", content.IdleText);
        Assert.False(content.HasIdleGlyph);
    }

    [Fact]
    public void Idle_can_be_time_only_or_hidden()
    {
        var content = new ClockContent();

        content.SetIdle(IdleDisplay.TimeOnly, []);
        Assert.True(content.IsIdleTimeOnly);
        Assert.True(content.IsIdleVisible);

        content.SetIdle(IdleDisplay.Nothing, []);
        Assert.False(content.IsIdleVisible);
    }

    [Fact]
    public async Task Playing_media_suggests_the_artwork_color_and_clears_it_when_paused()
    {
        var media = new FakeMediaService();
        var tint = new IslandTint();
        var changes = 0;
        tint.Changed += (_, _) => changes++;
        var module = new MediaModule(media, _recorder.Bus, new InlineDispatcher(), new FakeTimeProvider(), TestOptions.Create(), new FakeIslandSettings(), tint: tint);
        var artwork = SolidPng(Color.FromRgb(0x20, 0x60, 0xE0));
        await module.StartAsync(CancellationToken.None);

        media.Raise(FakeMediaService.Track("Música A") with { Thumbnail = artwork });

        var color = (Color)ColorConverter.ConvertFromString(Assert.IsType<string>(tint.Color));
        Assert.True(color.B > color.R && color.B > color.G);

        media.Raise(FakeMediaService.Track("Música A", MediaPlaybackState.Paused) with { Thumbnail = artwork });
        Assert.Null(tint.Color);
        Assert.Equal(2, changes);

        await module.StopAsync(CancellationToken.None);
    }

    private static byte[] SolidPng(Color color)
    {
        const int size = 16;
        var pixels = new byte[size * size * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = color.B;
            pixels[i + 1] = color.G;
            pixels[i + 2] = color.R;
            pixels[i + 3] = 255;
        }

        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}

using Dynasland.Core.Interfaces;
using Dynasland.Infrastructure.Windows;
using Dynasland.Infrastructure.Windows.Capture;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dynasland.Tests.Infrastructure;

/// <summary>
/// Captura e gravação reais da tela. Só rodam com DYNASLAND_CAPTURE_TESTS=1, porque mostram a borda
/// amarela de captura do Windows e gravam alguns segundos da tela de quem executa.
/// </summary>
public class CaptureHardwareTests
{
    private static bool Enabled => Environment.GetEnvironmentVariable("DYNASLAND_CAPTURE_TESTS") == "1";

    [Fact]
    public async Task Screenshot_of_primary_monitor_is_a_valid_png()
    {
        if (!Enabled)
        {
            return;
        }

        var service = new ScreenshotService(new MonitorService());
        var image = await service.CaptureScreenAsync(new MonitorService().GetMonitors().First(m => m.IsPrimary).DeviceName);

        Assert.True(image.Width > 100 && image.Height > 100);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, image.Png[..4]);
    }

    [Fact]
    public async Task Records_three_seconds_of_the_screen_with_system_audio()
    {
        if (!Enabled)
        {
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), "dynasland-tests", $"gravacao-{Guid.NewGuid():N}.mp4");
        var recorder = new ScreenRecorder(new MonitorService(), NullLogger<ScreenRecorder>.Instance);
        var completed = new TaskCompletionSource<RecordingResult>();
        recorder.Completed += (_, result) => completed.TrySetResult(result);

        await recorder.StartAsync(new RecordingOptions(new RecordingTarget(RecordingTargetKind.Monitor), path, 30, 6_000_000, SystemAudio: true, Microphone: false, ShowCursor: true));
        await Task.Delay(1500);
        recorder.Pause();
        await Task.Delay(500);
        recorder.Resume();
        await Task.Delay(1500);
        await recorder.StopAsync();

        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(result.Success, result.Error);
        Assert.True(result.SizeBytes > 10_000);
        Assert.InRange(result.Duration.TotalSeconds, 2.5, 4.5);
        if (Environment.GetEnvironmentVariable("DYNASLAND_KEEP_RECORDING") != "1")
        {
            File.Delete(result.Path!);
        }
    }
}

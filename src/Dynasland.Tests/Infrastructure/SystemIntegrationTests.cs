using Dynasland.Core.Models;
using Dynasland.Infrastructure.Windows.Audio;
using Dynasland.Infrastructure.Windows.Brightness;
using Dynasland.Infrastructure.Windows.Media;
using Dynasland.Infrastructure.Windows.Notifications;
using Dynasland.Infrastructure.Windows.Power;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dynasland.Tests.Infrastructure;

/// <summary>
/// Integração real com as APIs do Windows. Os resultados dependem do hardware e do que está
/// em execução; os testes verificam coerência, não valores específicos.
/// </summary>
public class SystemIntegrationTests
{
    [Fact]
    public async Task Core_audio_reads_default_device_and_reports_own_changes()
    {
        var service = new CoreAudioVolumeService(NullLogger<CoreAudioVolumeService>.Instance);
        if (!await service.StartAsync())
        {
            return; // Máquina sem saída de áudio.
        }

        try
        {
            var original = Assert.IsType<VolumeState>(service.Current);
            Assert.InRange(original.Level, 0f, 1f);

            // Variação de 1% e retorno ao valor original: deve chegar como alteração própria.
            var received = new TaskCompletionSource<VolumeChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            service.Changed += (_, e) => received.TrySetResult(e);
            var probe = original.Level < 0.99f ? original.Level + 0.01f : original.Level - 0.01f;

            service.SetLevel(probe);
            var change = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(VolumeChangeReason.Own, change.Reason);
            Assert.Equal(probe, change.State.Level, 0.005f);
            service.SetLevel(original.Level);
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Fact]
    public async Task Media_session_service_reads_current_session_consistently()
    {
        var service = new GsmtcMediaSessionService(TimeProvider.System, NullLogger<GsmtcMediaSessionService>.Instance);
        await service.StartAsync();
        try
        {
            var sessions = service.Sessions;
            Assert.Equal(sessions.Count, sessions.Select(s => s.Id).Distinct().Count());
            if (service.CurrentSessionId is { } current && sessions.Count > 0)
            {
                Assert.Equal(current, sessions[0].Id);
            }

            foreach (var snapshot in sessions)
            {
                Assert.False(string.IsNullOrWhiteSpace(snapshot.Title));
                Assert.False(string.IsNullOrWhiteSpace(snapshot.AppName));
                if (snapshot.Duration is { } duration)
                {
                    Assert.True(duration > TimeSpan.Zero);
                }
            }
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Fact]
    public async Task Power_service_reports_coherent_state()
    {
        var service = new WindowsPowerService();
        await service.StartAsync();
        try
        {
            var state = service.Current;
            Assert.InRange(state.Percent, 0, 100);
            if (!state.HasBattery)
            {
                Assert.True(state.IsPluggedIn);
                Assert.Null(state.RemainingTime);
            }
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Fact]
    public async Task Notification_service_starts_without_throwing()
    {
        var service = new WindowsNotificationService(TimeProvider.System, NullLogger<WindowsNotificationService>.Instance);

        var access = await service.StartAsync();
        await service.StopAsync();

        Assert.True(Enum.IsDefined(access));
    }

    [Fact]
    public async Task Brightness_service_reports_support_and_a_valid_level_when_supported()
    {
        var service = new WmiBrightnessService(NullLogger<WmiBrightnessService>.Instance);

        var supported = await service.StartAsync();
        await service.StopAsync();

        if (supported)
        {
            Assert.InRange(service.Current!.Value, 0, 100);
        }
        else
        {
            Assert.Null(service.Current);
        }
    }
}

using Dynasland.Core.Interfaces;

namespace Dynasland.Tests.Modules;

internal sealed class FakeAudioDevices : IAudioDeviceService
{
    public List<AudioDevice> Outputs { get; } = [];

    public List<string> DefaultsSet { get; } = [];

    public MicrophoneState? Microphone { get; set; }

    public event EventHandler<MicrophoneState?>? MicrophoneChanged;

    public event EventHandler? DevicesChanged;

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync() => Task.CompletedTask;

    public Task<IReadOnlyList<AudioDevice>> GetOutputDevicesAsync() => Task.FromResult<IReadOnlyList<AudioDevice>>([.. Outputs]);

    public Task<IReadOnlyList<AudioDevice>> GetInputDevicesAsync() => Task.FromResult<IReadOnlyList<AudioDevice>>([]);

    public Task<bool> SetDefaultDeviceAsync(string deviceId)
    {
        DefaultsSet.Add(deviceId);
        return Task.FromResult(true);
    }

    public void SetMicrophoneLevel(float level) => Microphone = Microphone! with { Level = level };

    public void SetMicrophoneMuted(bool muted)
    {
        Microphone = Microphone! with { IsMuted = muted };
        MicrophoneChanged?.Invoke(this, Microphone);
    }

    public void RaiseDevicesChanged() => DevicesChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class FakeExternalBrightness : IExternalBrightnessService
{
    public List<ExternalDisplayBrightness> Displays { get; } = [];

    public List<(string Id, int Percent)> Set { get; } = [];

    public Task<IReadOnlyList<ExternalDisplayBrightness>> GetDisplaysAsync() =>
        Task.FromResult<IReadOnlyList<ExternalDisplayBrightness>>([.. Displays]);

    public Task<bool> SetAsync(string displayId, int percent)
    {
        lock (Set)
        {
            Set.Add((displayId, percent));
        }

        return Task.FromResult(true);
    }
}

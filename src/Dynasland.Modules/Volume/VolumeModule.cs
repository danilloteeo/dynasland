using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Core.Shortcuts;

namespace Dynasland.Modules.Volume;

/// <summary>
/// Hardware Control de áudio: mostra o volume quando ele muda fora do Dynasland, permite controlá-lo,
/// trocar o dispositivo de saída padrão e controlar o microfone.
/// </summary>
public sealed class VolumeModule(
    IAudioVolumeService audio,
    IAudioDeviceService devices,
    IEventBus bus,
    IUiDispatcher ui,
    IModuleOptions options,
    IGlobalShortcutRegistry shortcuts) : IIslandModule
{
    public const string MicrophoneActivityId = "volume.microphone";

    private readonly List<IDisposable> _registrations = [];
    private StatusContent? _microphoneStatus;

    public const string ModuleId = "volume";
    public const string ActivityId = "volume.level";

    private static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(2.5);

    private VolumeContent? _content;

    public string Id => ModuleId;

    public string DisplayName => "Volume";

    public string Description => "Volume do sistema, troca do dispositivo de saída e controle do microfone.";

    public static readonly ModuleOption ShowOnChange = ModuleOption.Toggle(
        "showOnChange", "Mostrar ao mudar o volume", "Expande a ilha quando o volume é alterado pelas teclas ou por outro aplicativo.");

    public IReadOnlyList<ModuleOption> Options => [ShowOnChange];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var content = new VolumeContent(audio, devices);
        _content = content;
        _microphoneStatus = new StatusContent(StatusKind.Info, "Microfone");
        audio.Changed += OnVolumeChanged;

        if (!await audio.StartAsync(cancellationToken))
        {
            throw new ModuleUnavailableException("Nenhum dispositivo de saída de áudio encontrado.");
        }

        var initial = audio.Current;
        ui.Post(() => content.Apply(initial));

        // Microfone e lista de dispositivos são complementares: sem eles o volume continua funcionando.
        try
        {
            await devices.StartAsync(cancellationToken);
            devices.MicrophoneChanged += OnMicrophoneChanged;
            devices.DevicesChanged += OnDevicesChanged;
            var microphone = devices.Microphone;
            var outputs = await devices.GetOutputDevicesAsync();
            ui.Post(() =>
            {
                content.ApplyMicrophone(microphone);
                content.SetOutputs(outputs);
            });
        }
        catch (Exception)
        {
            ui.Post(() => content.ApplyMicrophone(null));
        }

        _registrations.Add(shortcuts.Register(new GlobalShortcut(
            "volume.microphoneMute", "Microfone: desligar/ligar", "Áudio",
            new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'M'), ToggleMicrophone)));
        _registrations.Add(shortcuts.Register(new GlobalShortcut(
            "volume.nextOutput", "Próximo dispositivo de saída", "Áudio", null, () => _ = NextOutputAsync())));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _registrations.Clear();
        audio.Changed -= OnVolumeChanged;
        devices.MicrophoneChanged -= OnMicrophoneChanged;
        devices.DevicesChanged -= OnDevicesChanged;
        await devices.StopAsync();
        await audio.StopAsync();
        _content = null;
    }

    private void OnMicrophoneChanged(object? sender, MicrophoneState? state)
    {
        if (_content is { } content)
        {
            ui.Post(() => content.ApplyMicrophone(state));
        }
    }

    private async void OnDevicesChanged(object? sender, EventArgs e)
    {
        if (_content is not { } content)
        {
            return;
        }

        try
        {
            var outputs = await devices.GetOutputDevicesAsync();
            ui.Post(() => content.SetOutputs(outputs));
        }
        catch (Exception)
        {
            // Lista desatualizada até a próxima mudança.
        }
    }

    /// <summary>Atalho global: liga/desliga o microfone e mostra o novo estado na ilha.</summary>
    private void ToggleMicrophone()
    {
        if (devices.Microphone is not { } microphone || _microphoneStatus is not { } status)
        {
            return;
        }

        var muted = !microphone.IsMuted;
        devices.SetMicrophoneMuted(muted);
        status.Kind = muted ? StatusKind.Error : StatusKind.Info;
        status.Title = muted ? "Microfone desligado" : "Microfone ligado";
        status.Message = microphone.DeviceName;
        bus.PostActivity(new IslandActivity(MicrophoneActivityId, ModuleId, ActivityKind.Transient, status)
        {
            Priority = ActivityPriority.High,
            Duration = DisplayDuration,
        });
    }

    /// <summary>Atalho global: passa para a próxima saída de áudio da lista.</summary>
    private async Task NextOutputAsync()
    {
        var outputs = await devices.GetOutputDevicesAsync();
        if (outputs.Count < 2)
        {
            return;
        }

        var current = outputs.ToList().FindIndex(o => o.IsDefault);
        var next = outputs[(current + 1) % outputs.Count];
        await devices.SetDefaultDeviceAsync(next.Id);
    }

    private void OnVolumeChanged(object? sender, VolumeChangedEventArgs e)
    {
        if (_content is not { } content)
        {
            return;
        }

        ui.Post(() => content.Apply(e.State));

        // Mudanças feitas pela própria ilha já estão visíveis para o usuário.
        if (e.Reason == VolumeChangeReason.Own || !options.IsOn(ModuleId, ShowOnChange))
        {
            return;
        }

        bus.PostActivity(new IslandActivity(ActivityId, ModuleId, ActivityKind.Transient, content)
        {
            Priority = ActivityPriority.High,
            Duration = DisplayDuration,
        });
    }
}

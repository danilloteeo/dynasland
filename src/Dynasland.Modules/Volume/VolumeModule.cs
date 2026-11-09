using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;

namespace Dynasland.Modules.Volume;

/// <summary>Mostra o volume quando ele muda fora do Dynasland e permite controlá-lo pela ilha.</summary>
public sealed class VolumeModule(IAudioVolumeService audio, IEventBus bus, IUiDispatcher ui, IModuleOptions options) : IIslandModule
{
    public const string ModuleId = "volume";
    public const string ActivityId = "volume.level";

    private static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(2.5);

    private VolumeContent? _content;

    public string Id => ModuleId;

    public string DisplayName => "Volume";

    public string Description => "Mostra e controla o volume do sistema.";

    public static readonly ModuleOption ShowOnChange = ModuleOption.Toggle(
        "showOnChange", "Mostrar ao mudar o volume", "Expande a ilha quando o volume é alterado pelas teclas ou por outro aplicativo.");

    public IReadOnlyList<ModuleOption> Options => [ShowOnChange];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var content = new VolumeContent(audio);
        _content = content;
        audio.Changed += OnVolumeChanged;

        if (!await audio.StartAsync(cancellationToken))
        {
            throw new ModuleUnavailableException("Nenhum dispositivo de saída de áudio encontrado.");
        }

        var initial = audio.Current;
        ui.Post(() => content.Apply(initial));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        audio.Changed -= OnVolumeChanged;
        await audio.StopAsync();
        _content = null;
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

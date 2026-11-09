using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;

namespace Dynasland.Modules.Downloads;

/// <summary>
/// Agrega os downloads de todos os provedores: atividade contínua enquanto houver downloads em
/// andamento (expande ao começar) e aviso com ações quando um download termina.
/// </summary>
public sealed class DownloadsModule(IEnumerable<IDownloadProvider> providers, IShellService shell, IEventBus bus, IUiDispatcher ui) : IIslandModule
{
    public const string ModuleId = "downloads";
    public const string ActiveActivityId = "downloads.active";

    private static readonly TimeSpan CompletedDisplayDuration = TimeSpan.FromSeconds(6);

    private readonly Lock _lock = new();
    private readonly HashSet<string> _active = [];
    private readonly List<IDownloadProvider> _running = [];
    private DownloadsContent? _content;

    public string Id => ModuleId;

    public string DisplayName => "Downloads";

    public string Description => "Acompanha downloads dos navegadores (Chrome, Edge, Opera, Firefox) na pasta Downloads.";

    public static string CompletedActivityId(string key) => $"download.done.{key}";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _content = new DownloadsContent();
            _active.Clear();
        }

        foreach (var provider in providers)
        {
            provider.Changed += OnDownloadChanged;
            if (await provider.StartAsync(cancellationToken))
            {
                _running.Add(provider);
            }
            else
            {
                provider.Changed -= OnDownloadChanged;
            }
        }

        if (_running.Count == 0)
        {
            throw new ModuleUnavailableException("Nenhuma fonte de downloads disponível (pasta Downloads não encontrada).");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var provider in _running)
        {
            provider.Changed -= OnDownloadChanged;
            await provider.StopAsync();
        }

        _running.Clear();
        lock (_lock)
        {
            _content = null;
            _active.Clear();
        }
    }

    private void OnDownloadChanged(object? sender, DownloadInfo info)
    {
        var key = $"{info.ProviderName}|{info.Id}";
        DownloadsContent content;
        bool firstActive = false, noneLeft = false;

        lock (_lock)
        {
            if (_content is null)
            {
                return;
            }

            content = _content;
            if (info.State == DownloadState.InProgress)
            {
                firstActive = _active.Count == 0;
                _active.Add(key);
            }
            else
            {
                noneLeft = _active.Remove(key) && _active.Count == 0;
            }
        }

        if (info.State == DownloadState.InProgress)
        {
            ui.Post(() => content.Upsert(key, info));
            if (firstActive)
            {
                bus.PostActivity(new IslandActivity(ActiveActivityId, ModuleId, ActivityKind.Live, content) { ExpandOnStart = true });
            }

            return;
        }

        ui.Post(() => content.Remove(key));
        if (noneLeft)
        {
            bus.RemoveActivity(ActiveActivityId);
        }

        if (info.State == DownloadState.Completed && info.FilePath is { } path)
        {
            var activityId = CompletedActivityId(key);
            var completed = new DownloadCompletedContent(
                info,
                open: () =>
                {
                    shell.Open(path);
                    bus.RemoveActivity(activityId);
                },
                reveal: () =>
                {
                    shell.RevealInFolder(path);
                    bus.RemoveActivity(activityId);
                });

            bus.PostActivity(new IslandActivity(activityId, ModuleId, ActivityKind.Transient, completed) { Duration = CompletedDisplayDuration });
        }
    }
}

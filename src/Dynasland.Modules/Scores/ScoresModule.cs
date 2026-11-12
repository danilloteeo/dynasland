using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Modules;
using Dynasland.Modules.Common;
using Microsoft.Extensions.Logging;

namespace Dynasland.Modules.Scores;

/// <summary>Placar dos jogos da NBA no dia. Os dados só são buscados com o painel aberto.</summary>
public sealed class ScoresModule(
    IScoresProvider provider,
    IEventBus bus,
    IUiDispatcher ui,
    TimeProvider time,
    IShortcutRegistry shortcuts,
    ILogger<ScoresModule> logger) : IIslandModule
{
    public const string ModuleId = "scores";
    public const string PanelActivityId = "scores.panel";

    private IDisposable? _registration;
    private ScoresContent? _content;

    public string Id => ModuleId;

    public string DisplayName => "Placar NBA";

    public string Description => "Jogos da NBA do dia, ao vivo e encerrados. Os dados vêm da ESPN e só são buscados com o painel aberto.";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _content = new ScoresContent(RefreshAsync);
        _registration = shortcuts.Register(new IslandShortcut("scores.panel", ModuleId, VectorIcons.Ball, "Placar NBA", OpenPanel));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _registration?.Dispose();
        _registration = null;
        _content = null;
        return Task.CompletedTask;
    }

    public void OpenPanel()
    {
        if (_content is not { } content)
        {
            return;
        }

        bus.PostActivity(new IslandActivity(PanelActivityId, ModuleId, ActivityKind.Transient, content) { OpenInteractive = true });
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_content is not { } content)
        {
            return;
        }

        try
        {
            var games = await provider.GetGamesAsync();
            var now = time.GetLocalNow();
            ui.Post(() => content.SetGames(games, now));
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "Placar indisponível.");
            ui.Post(() => content.SetError("Sem conexão"));
        }
    }
}

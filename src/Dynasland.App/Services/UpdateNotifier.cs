using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.App.ViewModels;
using Dynasland.Core.Activities;
using Dynasland.Core.Events;
using Dynasland.Core.Interfaces;
using Dynasland.Infrastructure.Updates;
using Microsoft.Extensions.Logging;

namespace Dynasland.App.Services;

/// <summary>
/// Mostra a atualização na ilha: verifica uma vez após a inicialização (se ligado), avisa da nova
/// versão, acompanha o download e, na partida seguinte, confirma a versão instalada.
/// </summary>
public sealed class UpdateNotifier
{
    public const string ActivityId = "app.update";

    /// <summary>Download e instalação: atividade contínua, fica até terminar.</summary>
    public const string ProgressActivityId = "app.update.progress";

    public const string CompletedActivityId = "app.update.completed";

    // Espera o computador terminar de iniciar antes de usar a rede.
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan NoticeDuration = TimeSpan.FromSeconds(10);

    private readonly IUpdateChecker _checker;
    private readonly IUpdateInstaller _installer;
    private readonly UpdateFlow _flow;
    private readonly IIslandSettingsService _settings;
    private readonly IEventBus _bus;
    private readonly IShellService _shell;
    private readonly ILogger<UpdateNotifier> _logger;
    private readonly UpdateAvailableContent _content;

    public UpdateNotifier(
        IUpdateChecker checker,
        IUpdateInstaller installer,
        UpdateFlow flow,
        IIslandSettingsService settings,
        IEventBus bus,
        IShellService shell,
        ILogger<UpdateNotifier> logger)
    {
        _checker = checker;
        _installer = installer;
        _flow = flow;
        _settings = settings;
        _bus = bus;
        _shell = shell;
        _logger = logger;
        _content = new UpdateAvailableContent(flow, dismiss: () => bus.RemoveActivity(ActivityId));
        flow.PropertyChanged += OnFlowChanged;
    }

    public async Task CheckAfterStartupAsync()
    {
        AnnounceCompletedUpdate();
        if (!_checker.IsConfigured || !_settings.Current.CheckForUpdates)
        {
            return;
        }

        try
        {
            await Task.Delay(StartupDelay);
            if (!_settings.Current.CheckForUpdates)
            {
                return;
            }

            var result = await _checker.CheckAsync();
            _logger.LogInformation("Verificação de atualizações: {Status} {Version}", result.Status, result.LatestVersion);
            _flow.Offer(result);
            if (_flow.Stage == UpdateStage.Available)
            {
                PostNotice();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha na verificação automática de atualizações.");
        }
    }

    /// <summary>Depois de uma atualização pelo aplicativo, confirma a versão nova ao reabrir.</summary>
    public void AnnounceCompletedUpdate()
    {
        if (_installer.ConsumeCompletedUpdate(GitHubUpdateChecker.CurrentVersion) is not { } version)
        {
            return;
        }

        var notes = _checker.SourceDescription is { } source ? $"https://{source}/tag/v{version}" : null;
        var content = new StatusContent(
            StatusKind.Info,
            "Dynasland atualizado",
            $"Versão {version} instalada.",
            notes is null ? null : new RelayCommand(() =>
            {
                _shell.OpenUrl(notes);
                _bus.RemoveActivity(CompletedActivityId);
            }),
            notes is null ? null : "Novidades");
        _bus.PostActivity(new IslandActivity(CompletedActivityId, "app", ActivityKind.Transient, content) { Duration = NoticeDuration });
    }

    private void OnFlowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(UpdateFlow.Stage))
        {
            return;
        }

        switch (_flow.Stage)
        {
            case UpdateStage.Downloading:
                // O aviso some sozinho; o progresso fica na ilha até o instalador assumir.
                _bus.RemoveActivity(ActivityId);
                _bus.PostActivity(new IslandActivity(ProgressActivityId, "app", ActivityKind.Live, _content)
                {
                    ExpandOnStart = true,
                    CanDismiss = false,
                });
                break;
            case UpdateStage.Failed:
                _bus.RemoveActivity(ProgressActivityId);
                PostNotice();
                break;
            case UpdateStage.None:
                _bus.RemoveActivity(ActivityId);
                _bus.RemoveActivity(ProgressActivityId);
                break;
        }
    }

    private void PostNotice() =>
        _bus.PostActivity(new IslandActivity(ActivityId, "app", ActivityKind.Transient, _content) { Duration = NoticeDuration });
}

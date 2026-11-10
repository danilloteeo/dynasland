using Dynasland.Core.Interfaces;
using Dynasland.Core.Models;
using Dynasland.Core.Shortcuts;
using Microsoft.Extensions.Logging;

namespace Dynasland.App.Services;

/// <summary>
/// Ações gerais da ilha, compartilhadas por atalhos globais, duplo clique e bandeja.
/// Registra no catálogo de atalhos as ações do próprio aplicativo (as dos módulos são registradas por eles).
/// </summary>
public sealed class IslandActions(
    IslandController controller,
    IIslandSettingsService settings,
    IMediaSessionService media,
    SettingsWindowService settingsWindow,
    IGlobalShortcutRegistry shortcuts,
    ILogger<IslandActions> logger) : IDisposable
{
    public const string Category = "Ilha";

    private readonly List<IDisposable> _registrations = [];

    public void RegisterShortcuts()
    {
        _registrations.Add(shortcuts.Register(new GlobalShortcut(
            "app.toggleIsland", "Abrir/fechar a ilha", Category,
            new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'D'), ToggleInteractive)));
        _registrations.Add(shortcuts.Register(new GlobalShortcut(
            "app.toggleVisibility", "Mostrar/ocultar a ilha", Category,
            new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'H'), ToggleVisibility)));
        _registrations.Add(shortcuts.Register(new GlobalShortcut(
            "app.playPause", "Reproduzir/pausar mídia", Category, null, PlayPauseMedia)));
        _registrations.Add(shortcuts.Register(new GlobalShortcut(
            "app.settings", "Abrir configurações", Category, null, settingsWindow.Show)));
    }

    public void ToggleInteractive()
    {
        if (!settings.Current.IsVisible)
        {
            // Atalho com a ilha oculta: mostra primeiro.
            ToggleVisibility();
            return;
        }

        controller.ToggleInteractive();
    }

    public void ToggleVisibility() => _ = Save(s => s with { IsVisible = !s.IsVisible });

    public void Run(DoubleClickAction action)
    {
        switch (action)
        {
            case DoubleClickAction.PlayPauseMedia:
                PlayPauseMedia();
                break;
            case DoubleClickAction.OpenSettings:
                settingsWindow.Show();
                break;
        }
    }

    /// <summary>Reproduz/pausa a sessão que o Windows considera atual (a mesma das teclas de mídia).</summary>
    public void PlayPauseMedia()
    {
        var sessions = media.Sessions;
        var session = sessions.FirstOrDefault(s => s.Id == media.CurrentSessionId) ?? sessions.FirstOrDefault();
        if (session is null)
        {
            return;
        }

        _ = session.IsPlaying ? media.PauseAsync(session.Id) : media.PlayAsync(session.Id);
    }

    public void Dispose()
    {
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _registrations.Clear();
    }

    private async Task Save(Func<IslandSettings, IslandSettings> change)
    {
        try
        {
            await settings.UpdateAsync(change);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao salvar configuração.");
        }
    }
}

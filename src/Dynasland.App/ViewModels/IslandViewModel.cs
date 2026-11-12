using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.App.Services;
using Dynasland.Core.Activities;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Core.Modules;
using Dynasland.Modules.Common;

namespace Dynasland.App.ViewModels;

public sealed partial class IslandViewModel : ObservableObject, IDisposable
{
    private readonly IslandController _controller;
    private readonly SettingsWindowService _settingsWindow;
    private readonly IIslandSettingsService _settings;
    private readonly IPrivacyIndicatorService _privacy;
    private readonly IUiDispatcher _ui;
    private readonly IShortcutRegistry _shortcuts;
    private readonly IDockBadges _badges;
    private readonly IGlanceRegistry _glances;
    private readonly IMediaSessionService _media;
    private readonly System.Windows.Threading.DispatcherTimer _pillClock;
    private IslandSnapshot _lastSnapshot = IslandSnapshot.Empty;
    private IActivityContent? _presented;
    private IslandState _presentedState;
    private bool _presentedVisible;
    private bool _windowVisible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMicrophoneDot), nameof(ShowCameraDot))]
    private IslandState _state;

    /// <summary>Algum aplicativo usa o microfone agora (indicador de privacidade).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMicrophoneDot))]
    private bool _microphoneInUse;

    /// <summary>Algum aplicativo usa a câmera agora (indicador de privacidade).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCameraDot))]
    private bool _cameraInUse;

    [ObservableProperty]
    private string _microphoneToolTip = string.Empty;

    [ObservableProperty]
    private string _cameraToolTip = string.Empty;

    [ObservableProperty]
    private IslandDimensions _dimensions = IslandLayout.Compact;

    [ObservableProperty]
    private IActivityContent? _compactContent;

    [ObservableProperty]
    private IActivityContent? _expandedContent;

    [ObservableProperty]
    private IActivityContent? _interactiveContent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGroupBadge))]
    private string _groupBadge = string.Empty;

    [ObservableProperty]
    private bool _canDismissFocused;

    [ObservableProperty]
    private bool _canPinFocused;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PinGlyph), nameof(PinToolTip))]
    private bool _isFocusedPinned;

    public IslandViewModel(
        IslandController controller,
        SettingsWindowService settingsWindow,
        IIslandSettingsService settings,
        IPrivacyIndicatorService privacy,
        IUiDispatcher ui,
        IShortcutRegistry shortcuts,
        IDockBadges badges,
        IGlanceRegistry glances,
        IMediaSessionService media)
    {
        _controller = controller;
        _settingsWindow = settingsWindow;
        _settings = settings;
        _privacy = privacy;
        _ui = ui;
        _shortcuts = shortcuts;
        _badges = badges;
        _glances = glances;
        _media = media;
        glances.Changed += OnPillSourcesChanged;
        media.Changed += OnPillSourcesChanged;
        _pillClock = new System.Windows.Threading.DispatcherTimer(TimeSpan.FromSeconds(5), System.Windows.Threading.DispatcherPriority.Background, (_, _) => UpdatePill(), System.Windows.Application.Current.Dispatcher);
        UpdatePill();
        badges.Changed += OnShortcutsChanged;
        controller.Changed += OnSnapshotChanged;
        shortcuts.Changed += OnShortcutsChanged;
        settings.Changed += OnSettingsChanged;
        privacy.Changed += OnPrivacyChanged;
        Apply(controller.Snapshot);
        ApplyPrivacy(privacy.Current);
    }

    /// <summary>Ponto laranja: só no compacto, na borda direita da cápsula.</summary>
    public bool ShowMicrophoneDot => MicrophoneInUse && State == IslandState.Compact;

    /// <summary>Ponto verde: só no compacto, na borda direita da cápsula.</summary>
    public bool ShowCameraDot => CameraInUse && State == IslandState.Compact;

    /// <summary>Ícones da dock (um por módulo ligado ou com atividade).</summary>
    public ObservableCollection<DockItem> Dock { get; } = [];

    /// <summary>Avisos esperando a vez; alimenta o selo "+N na fila" do cartão de notificação.</summary>
    [ObservableProperty]
    private int _queuedCount;

    /// <summary>Pula o aviso na tela e mostra o próximo da fila.</summary>
    [RelayCommand]
    private void Skip() => _controller.SkipCurrent();

    /// <summary>
    /// Pílula fechada do Volum: hora no meio, clima e a onda de música tocando na direita. Vale quando o indicador é o relógio
    /// ou a música; outras atividades (ex.: lembrete de água) continuam mostrando o próprio conteúdo compacto.
    /// </summary>
    [ObservableProperty]
    private bool _isClosedPill = true;

    [ObservableProperty]
    private string _pillTime = string.Empty;

    /// <summary>Alguma mídia está tocando: aparece a onda ao lado do clima.</summary>
    [ObservableProperty]
    private bool _musicPlaying;

    [ObservableProperty]
    private bool _hasWeather;

    /// <summary>Temperatura curta do clima atual, como "23°".</summary>
    [ObservableProperty]
    private string _weatherTemp = string.Empty;

    [ObservableProperty]
    private string _weatherGlyph = VectorIcons.Weather;

    private void OnPillSourcesChanged(object? sender, EventArgs e) => _ui.Post(UpdatePill);

    private void UpdatePill()
    {
        PillTime = DateTime.Now.ToString("HH:mm", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        MusicPlaying = _media.Sessions.Any(session => session.PlaybackState == MediaPlaybackState.Playing);

        var weather = _glances.Glances.FirstOrDefault(glance => glance.Id == "weather.now");
        var temp = weather?.ShortText?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        HasWeather = !string.IsNullOrEmpty(temp);
        WeatherTemp = temp ?? string.Empty;
        WeatherGlyph = weather?.Glyph ?? VectorIcons.Weather;
    }

    /// <summary>Há mais de uma página (módulo) para percorrer: aparecem as setas e as bolinhas.</summary>
    [ObservableProperty]
    private bool _hasPages;

    /// <summary>Data curta no alto da ilha aberta, como "qui., 12 nov".</summary>
    [ObservableProperty]
    private string _statusDate = string.Empty;

    /// <summary>Hora no alto da ilha aberta, como "14:52".</summary>
    [ObservableProperty]
    private string _statusTime = string.Empty;

    /// <summary>Atualiza a data e a hora do alto da ilha (a janela chama enquanto está aberta).</summary>
    public void UpdateStatusClock()
    {
        var now = DateTime.Now;
        var culture = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        StatusDate = now.ToString("ddd, d MMM", culture);
        StatusTime = now.ToString("HH:mm", culture);
    }

    [RelayCommand]
    private void PreviousPage() => StepPage(-1);

    [RelayCommand]
    private void NextPage() => StepPage(1);

    /// <summary>Seta ‹ ou ›: abre a página anterior (-1) ou a próxima (+1), dando a volta no fim.</summary>
    private void StepPage(int direction)
    {
        if (Dock.Count < 2)
        {
            return;
        }

        var current = Dock.ToList().FindIndex(item => item.IsActive);
        var next = (((current < 0 ? 0 : current + direction) % Dock.Count) + Dock.Count) % Dock.Count;
        OpenDockItem(Dock[next]);
    }

    /// <summary>Borda da tela: decide para que lado a cápsula cresce (definida pela janela).</summary>
    [ObservableProperty]
    private IslandEdge _edge = IslandEdge.Bottom;

    /// <summary>Clique num ícone da dock: abre a atividade do módulo, ou chama o atalho dele.</summary>
    [RelayCommand]
    private void OpenDockItem(DockItem? item)
    {
        if (item is null)
        {
            return;
        }

        // Como no modelo: clicar no módulo que já está aberto fecha o painel.
        if (item.IsActive && State == IslandState.Interactive)
        {
            CollapseInteractive();
            return;
        }

        // Abrir o módulo é "ver": o ponto de aviso some.
        _badges.Clear(item.ModuleId);
        if (item is { Kind: DockItemKind.Activity, ActivityId: { } activityId })
        {
            _controller.OpenActivity(activityId);
        }
        else if (_shortcuts.Shortcuts.FirstOrDefault(s => s.Id == item.ShortcutId) is { } shortcut)
        {
            shortcut.Invoke();
        }
    }

    public bool HasGroupBadge => GroupBadge.Length > 0;

    public string PinGlyph => IsFocusedPinned ? VectorIcons.PinOff : VectorIcons.Pin;

    public string PinToolTip => IsFocusedPinned ? "Desafixar do compacto" : "Fixar no compacto";

    [RelayCommand]
    private void Dismiss() => _controller.DismissFocused();

    /// <summary>Fixa o módulo da atividade em foco como indicador do compacto (ou desfaz).</summary>
    [RelayCommand]
    private async Task TogglePinAsync()
    {
        if (_controller.Snapshot.Focused is not { Kind: ActivityKind.Live } focused)
        {
            return;
        }

        var pinned = IsFocusedPinned ? null : focused.ModuleId;
        await _settings.UpdateAsync(s => s with { PinnedModuleId = pinned });
    }

    [RelayCommand]
    private void ToggleInteractive() => _controller.ToggleInteractive();

    [RelayCommand]
    private void OpenSettings() => _settingsWindow.Show();

    public void OnPointerEntered() => _controller.PointerEntered();

    public void OnPointerExited() => _controller.PointerExited();

    /// <summary>
    /// A janela foi exibida ou ocultada (pelo usuário, tela cheia, bloqueio). Oculta, nenhum conteúdo
    /// é considerado visível: o equalizador e os timers de progresso param.
    /// </summary>
    public void SetWindowVisible(bool visible)
    {
        if (_windowVisible != visible)
        {
            _controller.SetHidden(!visible);
        }

        _windowVisible = visible;
        NotifyPresentation(_presented, _presentedState);
    }

    /// <summary>Recolhe o estado interativo (clique fora da ilha).</summary>
    public void CollapseInteractive() => _controller.CollapseInteractive();

    private void OnSnapshotChanged(object? sender, IslandSnapshot snapshot) => Apply(snapshot);

    private void OnSettingsChanged(object? sender, IslandSettings settings) => _ui.Post(RebuildDock);

    // O registro avisa de qualquer thread.
    private void OnShortcutsChanged(object? sender, EventArgs e) => _ui.Post(RebuildDock);

    private void RebuildDock()
    {
        var snapshot = _lastSnapshot;
        var activities = new List<DockActivity>();
        if (snapshot.Focused is { } focusedActivity)
        {
            activities.Add(new DockActivity(focusedActivity.Id, focusedActivity.ModuleId));
        }

        if (snapshot.Indicator is { } indicator && activities.All(a => a.ActivityId != indicator.Id))
        {
            activities.Add(new DockActivity(indicator.Id, indicator.ModuleId));
        }

        for (var i = 0; i < snapshot.PageActivityIds.Count && i < snapshot.PageModules.Count; i++)
        {
            if (activities.All(a => a.ActivityId != snapshot.PageActivityIds[i]))
            {
                activities.Add(new DockActivity(snapshot.PageActivityIds[i], snapshot.PageModules[i]));
            }
        }

        var focusedModule = DockItems.FocusedModule(snapshot.State, snapshot.Indicator?.ModuleId, snapshot.Focused?.ModuleId);
        var items = DockItems.Build(
            _shortcuts.Shortcuts, activities, focusedModule, DockVisibility.Parse(_settings.Current.DockHiddenModules), _badges.Badges);
        if (!items.SequenceEqual(Dock))
        {
            Dock.Clear();
            foreach (var item in items)
            {
                Dock.Add(item);
            }
        }

        HasPages = Dock.Count > 1;

    }

    // O serviço avisa de uma thread do pool.
    private void OnPrivacyChanged(object? sender, PrivacyUsage usage) => _ui.Post(() => ApplyPrivacy(usage));

    private void ApplyPrivacy(PrivacyUsage usage)
    {
        MicrophoneToolTip = usage.Microphone ? "Microfone em uso: " + string.Join(", ", usage.MicrophoneApps) : string.Empty;
        CameraToolTip = usage.Camera ? "Câmera em uso: " + string.Join(", ", usage.CameraApps) : string.Empty;
        MicrophoneInUse = usage.Microphone;
        CameraInUse = usage.Camera;
    }

    private void Apply(IslandSnapshot snapshot)
    {
        // Conteúdo antes do estado, para a view animar a transição já com o conteúdo certo.
        // O conteúdo de um estado que está saindo é mantido até o fade terminar.
        CompactContent = snapshot.Indicator?.Content;
        IsClosedPill = snapshot.Indicator is null || snapshot.Indicator.ModuleId is "clock" or "media";
        if (snapshot.State == IslandState.Expanded)
        {
            ExpandedContent = snapshot.Focused?.Content;
        }
        else if (snapshot.State == IslandState.Interactive)
        {
            InteractiveContent = snapshot.Focused?.Content;
        }

        _lastSnapshot = snapshot;
        QueuedCount = snapshot.QueuedCount;
        RebuildDock();
        var focused = snapshot.Focused;
        GroupBadge = snapshot.State != IslandState.Compact && snapshot.FocusedGroupCount > 1 ? $"+{snapshot.FocusedGroupCount - 1}" : string.Empty;
        CanDismissFocused = snapshot.State == IslandState.Interactive && focused is { CanDismiss: true };
        CanPinFocused = snapshot.State == IslandState.Interactive && focused is { Kind: ActivityKind.Live };
        IsFocusedPinned = focused is not null && focused.ModuleId == _settings.Current.PinnedModuleId;
        State = snapshot.State;
        Dimensions = snapshot.Dimensions;

        NotifyPresentation(snapshot.State == IslandState.Compact ? snapshot.Indicator?.Content : snapshot.Focused?.Content, snapshot.State);
    }

    /// <summary>Avisa o conteúdo que ficou visível (e o que deixou de estar), para pausar trabalho invisível.</summary>
    private void NotifyPresentation(IActivityContent? content, IslandState state)
    {
        if (ReferenceEquals(content, _presented) && state == _presentedState && _windowVisible == _presentedVisible)
        {
            return;
        }

        if (_presented is not null && !ReferenceEquals(_presented, content))
        {
            _presented.OnPresentationChanged(null);
        }

        _presented = content;
        _presentedState = state;
        _presentedVisible = _windowVisible;
        content?.OnPresentationChanged(_windowVisible ? state : null);
    }

    public void Dispose()
    {
        _controller.Changed -= OnSnapshotChanged;
        _privacy.Changed -= OnPrivacyChanged;
        _shortcuts.Changed -= OnShortcutsChanged;
        _badges.Changed -= OnShortcutsChanged;
        _glances.Changed -= OnPillSourcesChanged;
        _media.Changed -= OnPillSourcesChanged;
        _pillClock.Stop();
        _settings.Changed -= OnSettingsChanged;
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.App.Services;
using Dynasland.Core.Activities;
using Dynasland.Core.Interfaces;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;

namespace Dynasland.App.ViewModels;

/// <summary>Indicador de uma aba do estado interativo.</summary>
public sealed record PageDot(int Index, bool IsCurrent);

public sealed partial class IslandViewModel : ObservableObject, IDisposable
{
    private readonly IslandController _controller;
    private readonly SettingsWindowService _settingsWindow;
    private readonly IIslandSettingsService _settings;
    private readonly IPrivacyIndicatorService _privacy;
    private readonly IUiDispatcher _ui;
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
    private int _pageIndex = -1;

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
        IUiDispatcher ui)
    {
        _controller = controller;
        _settingsWindow = settingsWindow;
        _settings = settings;
        _privacy = privacy;
        _ui = ui;
        controller.Changed += OnSnapshotChanged;
        privacy.Changed += OnPrivacyChanged;
        Apply(controller.Snapshot);
        ApplyPrivacy(privacy.Current);
    }

    /// <summary>Ponto laranja: só no compacto, na borda direita da cápsula.</summary>
    public bool ShowMicrophoneDot => MicrophoneInUse && State == IslandState.Compact;

    /// <summary>Ponto verde: só no compacto, na borda direita da cápsula.</summary>
    public bool ShowCameraDot => CameraInUse && State == IslandState.Compact;

    /// <summary>Pontos das abas; vazio quando há apenas uma.</summary>
    public ObservableCollection<PageDot> Pages { get; } = [];

    public bool HasPages => Pages.Count > 1;

    public bool HasGroupBadge => GroupBadge.Length > 0;

    public string PinGlyph => IsFocusedPinned ? "" : "";

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
    private void PreviousPage() => _controller.Cycle(-1);

    [RelayCommand]
    private void NextPage() => _controller.Cycle(1);

    [RelayCommand]
    private void SelectPage(int index) => _controller.SelectPage(index);

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
        if (snapshot.State == IslandState.Expanded)
        {
            ExpandedContent = snapshot.Focused?.Content;
        }
        else if (snapshot.State == IslandState.Interactive)
        {
            InteractiveContent = snapshot.Focused?.Content;
        }

        UpdatePages(snapshot);
        var focused = snapshot.Focused;
        GroupBadge = snapshot.State != IslandState.Compact && snapshot.FocusedGroupCount > 1 ? $"+{snapshot.FocusedGroupCount - 1}" : string.Empty;
        CanDismissFocused = snapshot.State == IslandState.Interactive && focused is { CanDismiss: true };
        CanPinFocused = snapshot.State == IslandState.Interactive && focused is { Kind: ActivityKind.Live };
        IsFocusedPinned = focused is not null && focused.ModuleId == _settings.Current.PinnedModuleId;
        State = snapshot.State;
        Dimensions = snapshot.Dimensions;

        NotifyPresentation(snapshot.State == IslandState.Compact ? snapshot.Indicator?.Content : snapshot.Focused?.Content, snapshot.State);
    }

    private void UpdatePages(IslandSnapshot snapshot)
    {
        if (snapshot.State == IslandState.Interactive && (Pages.Count != snapshot.PageCount || PageIndex != snapshot.PageIndex))
        {
            Pages.Clear();
            if (snapshot.PageCount > 1)
            {
                for (var i = 0; i < snapshot.PageCount; i++)
                {
                    Pages.Add(new PageDot(i, i == snapshot.PageIndex));
                }
            }

            PageIndex = snapshot.PageIndex;
            OnPropertyChanged(nameof(HasPages));
        }
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
    }
}

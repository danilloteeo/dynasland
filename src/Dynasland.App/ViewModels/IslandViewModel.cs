using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.App.Services;
using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;

namespace Dynasland.App.ViewModels;

/// <summary>Indicador de uma aba do estado interativo.</summary>
public sealed record PageDot(int Index, bool IsCurrent);

public sealed partial class IslandViewModel : ObservableObject, IDisposable
{
    private readonly IslandController _controller;
    private readonly SettingsWindowService _settingsWindow;
    private IActivityContent? _presented;
    private IslandState _presentedState;

    [ObservableProperty]
    private IslandState _state;

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

    public IslandViewModel(IslandController controller, SettingsWindowService settingsWindow)
    {
        _controller = controller;
        _settingsWindow = settingsWindow;
        controller.Changed += OnSnapshotChanged;
        Apply(controller.Snapshot);
    }

    /// <summary>Pontos das abas; vazio quando há apenas uma.</summary>
    public ObservableCollection<PageDot> Pages { get; } = [];

    public bool HasPages => Pages.Count > 1;

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

    public void Cycle(int direction) => _controller.Cycle(direction);

    public void OnPointerEntered() => _controller.PointerEntered();

    public void OnPointerExited() => _controller.PointerExited();

    /// <summary>Volta ao compacto e descarta o que estava pendente (ex.: ilha ocultada).</summary>
    public void Collapse() => _controller.Reset();

    private void OnSnapshotChanged(object? sender, IslandSnapshot snapshot) => Apply(snapshot);

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
        if (ReferenceEquals(content, _presented) && state == _presentedState)
        {
            return;
        }

        if (_presented is not null && !ReferenceEquals(_presented, content))
        {
            _presented.OnPresentationChanged(null);
        }

        _presented = content;
        _presentedState = state;
        content?.OnPresentationChanged(state);
    }

    public void Dispose() => _controller.Changed -= OnSnapshotChanged;
}

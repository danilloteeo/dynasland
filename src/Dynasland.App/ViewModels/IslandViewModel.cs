using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.App.Services;
using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;

namespace Dynasland.App.ViewModels;

public sealed partial class IslandViewModel : ObservableObject, IDisposable
{
    private readonly IslandController _controller;

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

    public IslandViewModel(IslandController controller)
    {
        _controller = controller;
        controller.Changed += OnSnapshotChanged;
        Apply(controller.Snapshot);
    }

    [RelayCommand]
    private void ToggleInteractive() => _controller.ToggleInteractive();

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

        State = snapshot.State;
        Dimensions = snapshot.Dimensions;
    }

    public void Dispose() => _controller.Changed -= OnSnapshotChanged;
}

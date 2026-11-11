using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dynasland.Core.Activities;
using Dynasland.Core.Layout;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Water;

/// <summary>
/// Painel da água aberto pelo atalho da ilha: um corpo que se enche conforme os copos do dia
/// (desenhado por <see cref="WaterBodyMeter"/>), com Bebi um copo e Desfazer.
/// </summary>
/// <remarks>Conteúdo vivo: o módulo chama <see cref="Apply"/> na thread de UI a cada mudança da contagem ou da meta.</remarks>
public sealed partial class WaterBodyPanelContent(Action drink, Action undo, Action<IslandState?> presentationChanged)
    : ObservableObject, IActivityContent
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Count), nameof(ProgressText), nameof(Message), nameof(GoalReached))]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private int _glasses;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Count), nameof(ProgressText), nameof(Message), nameof(GoalReached))]
    private int _goal = 8;

    /// <summary>Nível da água, N/M limitado a 1 (0 = pés, 1 = topo da cabeça).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PercentText))]
    private double _progress;

    public string Glyph => VectorIcons.Water;

    public string Title => "Água";

    public string DrinkLabel => "Bebi um copo";

    public string UndoLabel => "Desfazer";

    /// <summary>Contagem curta para o estado compacto, ex.: "3/8".</summary>
    public string Count => $"{Glasses}/{Goal}";

    public string PercentText => $"{(int)Math.Round(Progress * 100, MidpointRounding.AwayFromZero)}%";

    public string ProgressText => $"{Glasses} de {Goal} copos hoje";

    public bool GoalReached => Glasses >= Goal;

    public string Message => GoalReached
        ? "Meta de hoje batida!"
        : Goal - Glasses == 1 ? "Falta 1 copo para a meta." : $"Faltam {Goal - Glasses} copos para a meta.";

    /// <summary>Atualiza a contagem e a meta (na thread de UI).</summary>
    public void Apply(int glasses, int goal)
    {
        var safeGoal = Math.Max(1, goal);
        Goal = safeGoal;
        Progress = Math.Clamp(glasses / (double)safeGoal, 0, 1);
        Glasses = Math.Max(0, glasses);
    }

    public IslandDimensions? GetPreferredDimensions(IslandState state) => state switch
    {
        IslandState.Compact => new IslandDimensions(200, 34, 17),
        IslandState.Expanded => new IslandDimensions(320, 64, 30),
        IslandState.Interactive => new IslandDimensions(400, 240, 36),
        _ => null,
    };

    public void OnPresentationChanged(IslandState? state) => presentationChanged(state);

    [RelayCommand]
    private void Drink() => drink();

    private bool CanUndo() => Glasses > 0;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => undo();
}

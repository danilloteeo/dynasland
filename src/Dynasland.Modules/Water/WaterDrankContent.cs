using Dynasland.Core.Activities;
using Dynasland.Modules.Common;

namespace Dynasland.Modules.Water;

/// <summary>
/// Confirmação animada de um copo registrado: a gota enche do progresso anterior ao novo
/// (desenhada por <see cref="WaterFillMeter"/>).
/// </summary>
public sealed class WaterDrankContent : IActivityContent
{
    public WaterDrankContent(int glasses, int goal)
    {
        var safeGoal = Math.Max(1, goal);
        Glasses = glasses;
        Goal = goal;
        GoalReached = glasses >= goal;
        Title = GoalReached ? "Meta de água batida!" : "Boa!";
        Message = $"{glasses} de {goal} copos hoje";
        FromProgress = Math.Clamp((glasses - 1) / (double)safeGoal, 0, 1);
        ToProgress = Math.Clamp(glasses / (double)safeGoal, 0, 1);
    }

    public string Icon => VectorIcons.Water;

    public int Glasses { get; }

    public int Goal { get; }

    /// <summary>Contagem curta para o estado compacto, ex.: "3/8".</summary>
    public string Count => $"{Glasses}/{Goal}";

    public string Title { get; }

    public string Message { get; }

    /// <summary>Progresso antes deste copo, (N−1)/M entre 0 e 1.</summary>
    public double FromProgress { get; }

    /// <summary>Progresso com este copo, N/M limitado a 1.</summary>
    public double ToProgress { get; }

    public bool GoalReached { get; }
}

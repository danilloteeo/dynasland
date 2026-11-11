namespace Dynasland.Core.Activities;

/// <summary>Decide se uma atividade recém-publicada faz o anel de alerta da ilha pulsar.</summary>
public static class IslandAlertPulse
{
    /// <summary>
    /// Só avisos transitórios de prioridade alta (ou crítica) pulsam: lembretes, fim de timer, bateria baixa.
    /// Atividades contínuas e avisos comuns não chamam atenção extra.
    /// </summary>
    public static bool ShouldPulse(IslandActivity activity) =>
        activity.Kind == ActivityKind.Transient && activity.Priority >= ActivityPriority.High;
}

namespace Dynasland.Core.Activities;

public enum ActivityRecordState
{
    /// <summary>Aguardando na fila (outra atividade está visível ou a ilha está oculta).</summary>
    Queued,
    Shown,

    /// <summary>Substituída por uma atividade mais recente do mesmo grupo.</summary>
    Grouped,

    /// <summary>Exibida pelo tempo previsto.</summary>
    Completed,

    /// <summary>Fechada pelo usuário.</summary>
    Dismissed,

    /// <summary>Retirada pelo módulo (ex.: download terminou, módulo desativado).</summary>
    Removed,

    /// <summary>
    /// Descartada sem ser vista porque a ilha ficou oculta (tela cheia, bloqueio) e a prioridade era
    /// baixa. Fica registrada aqui, nunca some em silêncio.
    /// </summary>
    Dropped,
}

/// <summary>Registro do ciclo de vida de uma atividade publicada na ilha.</summary>
public sealed record ActivityRecord(
    string Id,
    string ModuleId,
    ActivityKind Kind,
    ActivityPriority Priority,
    DateTimeOffset OccurredAt,
    ActivityRecordState State,
    DateTimeOffset UpdatedAt);

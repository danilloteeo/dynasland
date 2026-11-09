namespace Dynasland.Core.Activities;

/// <summary>
/// Algo que um módulo quer apresentar na ilha. Publicar novamente o mesmo <see cref="Id"/>
/// atualiza a atividade existente em vez de criar outra.
/// </summary>
public sealed record IslandActivity(string Id, string ModuleId, ActivityKind Kind, IActivityContent Content)
{
    public ActivityPriority Priority { get; init; } = ActivityPriority.Normal;

    /// <summary>Tempo expandida; <c>null</c> usa a configuração do usuário.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>Para atividades <see cref="ActivityKind.Live"/>: expande a ilha quando a atividade começa.</summary>
    public bool ExpandOnStart { get; init; }
}

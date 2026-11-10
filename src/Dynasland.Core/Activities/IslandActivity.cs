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

    /// <summary>
    /// Para atividades <see cref="ActivityKind.Transient"/> pedidas pelo usuário (ex.: atalho da tela inicial):
    /// abre direto no estado interativo e fica até o usuário fechar ou a ilha recolher por inatividade.
    /// </summary>
    public bool OpenInteractive { get; init; }

    /// <summary>
    /// Atividades transitórias com a mesma chave são agrupadas: a mais recente substitui a anterior
    /// ainda pendente ou visível, e a contagem fica em <see cref="IslandSnapshot.FocusedGroupCount"/>
    /// (ex.: várias mensagens do mesmo aplicativo).
    /// </summary>
    public string? GroupKey { get; init; }

    /// <summary>Pode ser fechada pelo usuário (botão no rodapé). Contínuas fechadas ficam ocultas até o módulo republicá-las com outro Id.</summary>
    public bool CanDismiss { get; init; } = true;
}

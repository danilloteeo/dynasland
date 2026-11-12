using Dynasland.Core.Layout;
using Dynasland.Core.Models;

namespace Dynasland.Core.Activities;

/// <summary>O que a ilha deve mostrar agora.</summary>
/// <param name="State">Estado visual.</param>
/// <param name="Focused">Atividade em destaque (expandida ou interativa); <c>null</c> no compacto.</param>
/// <param name="Indicator">Atividade contínua de maior prioridade, para o indicador do estado compacto.</param>
/// <param name="Dimensions">Tamanho da cápsula para o estado e conteúdo atuais.</param>
/// <param name="LiveCount">Quantidade de atividades contínuas.</param>
/// <param name="PageCount">No estado interativo: quantas atividades podem ser alternadas (abas).</param>
/// <param name="PageIndex">No estado interativo: posição da atividade em foco; -1 fora dele.</param>
/// <param name="FocusedGroupCount">Quantas atividades do mesmo grupo a atividade em foco representa (1 = só ela).</param>
/// <param name="IndicatorPinned">O indicador do compacto é o módulo fixado pelo usuário.</param>
public sealed record IslandSnapshot(
    IslandState State,
    IslandActivity? Focused,
    IslandActivity? Indicator,
    IslandDimensions Dimensions,
    int LiveCount,
    int PageCount = 0,
    int PageIndex = -1,
    int FocusedGroupCount = 1,
    bool IndicatorPinned = false)
{
    /// <summary>No estado interativo: o módulo de cada aba, na ordem das abas (identifica o ícone da aba); vazio fora dele.</summary>
    public PageModuleList PageModules { get; init; } = PageModuleList.Empty;

    /// <summary>Avisos esperando a vez (fila de transitórias), para o selo "+N na fila".</summary>
    public int QueuedCount { get; init; }

    /// <summary>No estado interativo: o id da atividade de cada aba, na mesma ordem de <see cref="PageModules"/>; vazio fora dele.</summary>
    public PageModuleList PageActivityIds { get; init; } = PageModuleList.Empty;

    public static IslandSnapshot Empty { get; } = new(IslandState.Compact, null, null, IslandLayout.Compact, 0);
}

using Dynasland.Core.Layout;
using Dynasland.Core.Models;

namespace Dynasland.Core.Activities;

/// <summary>O que a ilha deve mostrar agora.</summary>
/// <param name="State">Estado visual.</param>
/// <param name="Focused">Atividade em destaque (expandida ou interativa); <c>null</c> no compacto.</param>
/// <param name="Indicator">Atividade contínua de maior prioridade, para o indicador do estado compacto.</param>
/// <param name="Dimensions">Tamanho da cápsula para o estado e conteúdo atuais.</param>
/// <param name="LiveCount">Quantidade de atividades contínuas.</param>
public sealed record IslandSnapshot(
    IslandState State,
    IslandActivity? Focused,
    IslandActivity? Indicator,
    IslandDimensions Dimensions,
    int LiveCount)
{
    public static IslandSnapshot Empty { get; } = new(IslandState.Compact, null, null, IslandLayout.Compact, 0);
}

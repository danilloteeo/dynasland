using Dynasland.Core.Activities;
using Dynasland.Core.Models;
using Dynasland.Modules.Common;

namespace Dynasland.App.ViewModels;

/// <summary>Uma aba do estado interativo: ponto ou ícone do módulo, conforme a barra couber.</summary>
/// <param name="Glyph">Glifo de fonte ou chave "vector:..." do módulo; <c>null</c> se o módulo não tem ícone.</param>
public sealed record PageTab(int Index, bool IsCurrent, string? Glyph, string AccessibleName)
{
    /// <summary>Desenha o ícone do módulo no lugar do ponto (definido pela barra ao decidir que cabem).</summary>
    public bool ShowIcon { get; init; }

    public bool HasGlyph => Glyph is not null;
}

/// <summary>Monta as abas do rodapé a partir do instantâneo da ilha e decide se cabem como ícones.</summary>
public static class PageTabBar
{
    /// <summary>Espaço do rodapé tomado pelos botões laterais e pelas setas.</summary>
    private const double FooterReserve = 120;

    /// <summary>Largura de cada aba com ícone.</summary>
    private const double TabWidth = 40;

    /// <summary>Abas do instantâneo; vazio fora do interativo ou com uma só página.</summary>
    public static IReadOnlyList<PageTab> PagesFor(IslandSnapshot snapshot)
    {
        if (snapshot.State != IslandState.Interactive || snapshot.PageCount <= 1)
        {
            return [];
        }

        var tabs = new List<PageTab>(snapshot.PageCount);
        for (var i = 0; i < snapshot.PageCount; i++)
        {
            var icon = i < snapshot.PageModules.Count ? PageIcons.For(snapshot.PageModules[i]) : null;
            tabs.Add(new PageTab(
                i,
                i == snapshot.PageIndex,
                icon?.Glyph,
                icon is null ? $"Ir para a página {i + 1}" : $"Ir para {icon.Name}"));
        }

        return tabs;
    }

    /// <summary>Ícones só quando todas as abas têm um e cabem na largura da ilha; senão, pontos.</summary>
    public static bool ShowsIcons(IReadOnlyList<PageTab> tabs, double islandWidth) =>
        tabs.Count > 0
        && tabs.All(t => t.HasGlyph)
        && tabs.Count <= Math.Floor((islandWidth - FooterReserve) / TabWidth);
}

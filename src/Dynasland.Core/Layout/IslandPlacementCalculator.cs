using Dynasland.Core.Models;

namespace Dynasland.Core.Layout;

/// <summary>
/// Posição da janela da ilha. A janela tem largura fixa e só a cápsula dentro dela muda de tamanho:
/// no centro a cápsula fica centralizada na janela; à esquerda/direita a janela encosta na borda da
/// área útil e a cápsula se alinha ao mesmo lado, a <see cref="EdgeMargin"/> da borda. Assim a borda
/// ancorada não se mexe enquanto a largura anima, sem mover o HWND a cada quadro.
/// </summary>
public static class IslandPlacementCalculator
{
    /// <summary>Distância da cápsula até a borda lateral da tela nas posições esquerda/direita, em DIPs.</summary>
    public const double EdgeMargin = 16;

    /// <summary>
    /// Calcula a posição da janela centralizada horizontalmente no topo da área útil.
    /// Todos os valores estão em pixels físicos.
    /// </summary>
    public static PixelPoint TopCenter(PixelRect workArea, int windowWidth, double topMargin) =>
        Place(workArea, windowWidth, IslandPosition.Center, topMargin);

    /// <summary>
    /// Posição da janela no topo da área útil: centralizada, encostada na borda esquerda ou na direita.
    /// Todos os valores estão em pixels físicos.
    /// </summary>
    public static PixelPoint Place(PixelRect workArea, int windowWidth, IslandPosition position, double topMargin)
    {
        var x = position switch
        {
            IslandPosition.Left => workArea.Left,
            IslandPosition.Right => workArea.Right - windowWidth,
            _ => workArea.Left + (int)Math.Round((workArea.Width - windowWidth) / 2.0),
        };
        var y = workArea.Top + (int)Math.Round(Math.Max(0, topMargin));
        return new PixelPoint(x, y);
    }

    /// <summary>
    /// Posição da janela ancorada na borda de baixo (acima da barra de tarefas, pela área útil) ou na de cima.
    /// A margem é a distância até a borda escolhida. Valores em pixels físicos.
    /// </summary>
    public static PixelPoint PlaceOnEdge(PixelRect workArea, int windowWidth, int windowHeight, IslandPosition position, IslandEdge edge, double margin)
    {
        var x = Place(workArea, windowWidth, position, 0).X;
        var offset = (int)Math.Round(Math.Max(0, margin));
        var y = edge == IslandEdge.Top
            ? workArea.Top + offset
            : workArea.Bottom - windowHeight - offset;
        return new PixelPoint(x, y);
    }

    /// <summary>Distância do topo (DIPs): a ilha encaixada fica colada na borda de cima.</summary>
    public static double Top(IslandShape shape, double topMargin) =>
        shape == IslandShape.Notch ? 0 : topMargin;

    /// <summary>
    /// Borda esquerda da cápsula dentro da janela (DIPs), o que o alinhamento da cápsula reproduz:
    /// centro mantém o meio, esquerda mantém a borda esquerda e direita mantém a borda direita.
    /// </summary>
    public static double IslandLeft(IslandPosition position, double windowWidth, double islandWidth, double edgeMargin) => position switch
    {
        IslandPosition.Left => edgeMargin,
        IslandPosition.Right => windowWidth - edgeMargin - islandWidth,
        _ => (windowWidth - islandWidth) / 2,
    };
}

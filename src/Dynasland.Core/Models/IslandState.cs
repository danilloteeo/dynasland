namespace Dynasland.Core.Models;

public enum IslandState
{
    /// <summary>Cápsula pequena no topo, apenas com indicadores discretos.</summary>
    Compact,

    /// <summary>Expandida automaticamente para apresentar um evento.</summary>
    Expanded,

    /// <summary>Expandida pelo usuário, com controles adicionais.</summary>
    Interactive,
}
